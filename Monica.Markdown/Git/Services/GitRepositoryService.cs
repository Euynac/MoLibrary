using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.Markdown.Git.Events;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Git.Modules;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Modules;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// Default repository synchronization service backed by LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryService(
    IOptions<ModuleGitOption> options,
    GitCredentialManager credentialManager,
    IServiceProvider serviceProvider,
    IMoLocalEventBus localEventBus,
    ILogger<GitRepositoryService> logger) : IGitRepositoryService
{
    private readonly ModuleGitOption _option = options.Value;
    private readonly Dictionary<string, GitRepositoryRegistration> _repositories = options.Value
        .RepositoryRegistrations
        .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _bindings = options.Value.RepositoryBindings
        .GroupBy(x => x.RepositoryId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            x => x.Key,
            x => x.Select(y => y.DocumentGroupKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(y => y, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitRepositoryRuntimeState> _runtimeStates = options.Value
        .RepositoryRegistrations
        .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            x => x.Id,
            _ => new GitRepositoryRuntimeState(),
            StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<IReadOnlyList<GitRepositoryStatus>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GitRepositoryStatus> result = _repositories.Values
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(CreateSnapshot)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<GitRepositoryStatus> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        var registration = GetRegistration(repositoryId);
        return Task.FromResult(CreateSnapshot(registration));
    }

    /// <inheritdoc />
    public Task<GitRepositoryStatus?> FindRepositoryByRemoteUrlAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        var normalized = GitRemoteUrlNormalizer.Normalize(remoteUrl);
        var registration = _repositories.Values.FirstOrDefault(x =>
            string.Equals(
                GitRemoteUrlNormalizer.Normalize(x.RemoteUrl),
                normalized,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(registration is null ? null : CreateSnapshot(registration));
    }

    /// <inheritdoc />
    public async Task<GitSyncResult> SyncRepositoryAsync(
        string repositoryId,
        GitSyncTrigger trigger = GitSyncTrigger.Manual,
        CancellationToken cancellationToken = default)
    {
        var registration = GetRegistration(repositoryId);
        var state = _runtimeStates[registration.Id];
        var gate = GetLock(registration.Id);

        await gate.WaitAsync(cancellationToken);
        try
        {
            state.State = GitRepositorySyncState.Syncing;
            state.LastError = null;
            state.LastSyncMessage = null;

            var resolvedLocalPath = Path.GetFullPath(registration.LocalPath);
            var previousCommit = state.CurrentCommit;

            try
            {
                var resolvedCredential = await credentialManager.ResolveAsync(registration, cancellationToken);
                using var repository = OpenOrCloneRepository(registration, resolvedLocalPath, resolvedCredential);

                var remote = repository.Network.Remotes["origin"]
                             ?? repository.Network.Remotes.FirstOrDefault()
                             ?? throw new InvalidOperationException(
                                 $"Repository '{registration.Id}' does not contain a Git remote.");

                FetchRemote(repository, remote, resolvedCredential);

                var targetBranchName = ResolveTargetBranchName(repository, remote.Name, registration.Branch);
                if (string.IsNullOrWhiteSpace(targetBranchName))
                {
                    throw new InvalidOperationException(
                        $"Unable to determine the target branch for repository '{registration.Id}'.");
                }

                var remoteBranch = repository.Branches[$"{remote.Name}/{targetBranchName}"]
                                   ?? throw new InvalidOperationException(
                                       $"Remote branch '{remote.Name}/{targetBranchName}' was not found.");

                var localBranch = repository.Branches[targetBranchName]
                                 ?? repository.CreateBranch(targetBranchName, remoteBranch.Tip);

                Commands.Checkout(repository, localBranch, new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force
                });

                var wasDirty = repository.RetrieveStatus().IsDirty;
                repository.Reset(ResetMode.Hard, remoteBranch.Tip);
                repository.RemoveUntrackedFiles();

                var currentCommit = repository.Head.Tip?.Sha;
                var hasChanges = !string.Equals(previousCommit, currentCommit, StringComparison.Ordinal);
                var refreshMessage = await RefreshBoundDocumentGroupsAsync(registration.Id, cancellationToken);

                state.State = GitRepositorySyncState.Ready;
                state.IsAvailable = true;
                state.IsDirty = repository.RetrieveStatus().IsDirty;
                state.CurrentCommit = currentCommit;
                state.LastSyncAtUtc = DateTimeOffset.UtcNow;
                state.ResolvedBranch = targetBranchName;
                state.LastSyncMessage = BuildSyncMessage(trigger, hasChanges, wasDirty, refreshMessage);

                await PublishUpdatedEventAsync(registration, previousCommit, currentCommit, hasChanges, trigger, targetBranchName, cancellationToken);

                logger.LogInformation(
                    "Git repository '{RepositoryId}' synchronized via {Trigger}. Branch={Branch}, Commit={Commit}",
                    registration.Id,
                    trigger,
                    targetBranchName,
                    currentCommit);

                return new GitSyncResult
                {
                    RepositoryId = registration.Id,
                    Trigger = trigger,
                    Success = true,
                    HasChanges = hasChanges,
                    PreviousCommit = previousCommit,
                    CurrentCommit = currentCommit,
                    Message = state.LastSyncMessage ?? "Synchronization completed."
                };
            }
            catch (Exception ex)
            {
                state.State = GitRepositorySyncState.Failed;
                state.IsAvailable = Directory.Exists(resolvedLocalPath) && Repository.IsValid(resolvedLocalPath);
                state.LastError = ex.Message;
                state.LastSyncMessage = ex.Message;

                logger.LogError(ex, "Failed to synchronize Git repository '{RepositoryId}'", registration.Id);

                return new GitSyncResult
                {
                    RepositoryId = registration.Id,
                    Trigger = trigger,
                    Success = false,
                    HasChanges = false,
                    PreviousCommit = previousCommit,
                    CurrentCommit = state.CurrentCommit,
                    Message = ex.Message
                };
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitSyncResult>> SyncAllAsync(
        GitSyncTrigger trigger = GitSyncTrigger.Manual,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GitSyncResult>();

        foreach (var repositoryId in _repositories.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await SyncRepositoryAsync(repositoryId, trigger, cancellationToken));
        }

        return results;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GitCredentialInfo>> GetCredentialsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(credentialManager.GetCredentialInfos());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GitCredentialResolverInfo>> GetCredentialResolversAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(credentialManager.GetResolverInfos());
    }

    private GitRepositoryRegistration GetRegistration(string repositoryId)
    {
        if (_repositories.TryGetValue(repositoryId, out var registration))
        {
            return registration;
        }

        throw new KeyNotFoundException($"Git repository '{repositoryId}' is not registered.");
    }

    private SemaphoreSlim GetLock(string repositoryId)
    {
        lock (_locks)
        {
            if (_locks.TryGetValue(repositoryId, out var gate))
            {
                return gate;
            }

            gate = new SemaphoreSlim(1, 1);
            _locks[repositoryId] = gate;
            return gate;
        }
    }

    private GitRepositoryStatus CreateSnapshot(GitRepositoryRegistration registration)
    {
        var state = _runtimeStates[registration.Id];
        var resolvedLocalPath = Path.GetFullPath(registration.LocalPath);
        var boundGroups = GetBoundDocumentGroups(registration.Id);
        var validGroups = GetRegisteredMarkdownGroupKeys();
        var missingGroups = boundGroups
            .Where(x => !validGroups.Contains(x))
            .ToArray();

        return new GitRepositoryStatus
        {
            Id = registration.Id,
            Provider = registration.Provider,
            RemoteUrl = registration.RemoteUrl,
            LocalPath = registration.LocalPath,
            ResolvedLocalPath = resolvedLocalPath,
            ConfiguredBranch = registration.Branch,
            ResolvedBranch = state.ResolvedBranch,
            CredentialId = registration.CredentialId,
            State = state.State,
            IsAvailable = state.IsAvailable,
            IsDirty = state.IsDirty,
            CurrentCommit = state.CurrentCommit,
            LastSyncAtUtc = state.LastSyncAtUtc,
            LastSyncMessage = state.LastSyncMessage,
            LastError = state.LastError,
            BoundDocumentGroupKeys = boundGroups,
            MissingDocumentGroupKeys = missingGroups
        };
    }

    private IReadOnlyList<string> GetBoundDocumentGroups(string repositoryId)
    {
        return _bindings.TryGetValue(repositoryId, out var bindings)
            ? bindings
            : Array.Empty<string>();
    }

    private HashSet<string> GetRegisteredMarkdownGroupKeys()
    {
        var markdownOptions = serviceProvider.GetService<IOptions<ModuleMarkdownOption>>();
        return markdownOptions?.Value.DocumentGroupRegistrations
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];
    }

    private Repository OpenOrCloneRepository(
        GitRepositoryRegistration registration,
        string resolvedLocalPath,
        GitResolvedCredential? resolvedCredential)
    {
        if (!Repository.IsValid(resolvedLocalPath))
        {
            if (Directory.Exists(resolvedLocalPath)
                && Directory.EnumerateFileSystemEntries(resolvedLocalPath).Any())
            {
                throw new InvalidOperationException(
                    $"Local path '{resolvedLocalPath}' exists but is not an empty Git repository path.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resolvedLocalPath) ?? resolvedLocalPath);

            var cloneOptions = new CloneOptions
            {
                BranchName = registration.Branch
            };
            cloneOptions.FetchOptions.CredentialsProvider = CreateCredentialsProvider(resolvedCredential);

            Repository.Clone(registration.RemoteUrl, resolvedLocalPath, cloneOptions);
        }

        return new Repository(resolvedLocalPath);
    }

    private static string BuildSyncMessage(
        GitSyncTrigger trigger,
        bool hasChanges,
        bool wasDirty,
        string? refreshMessage)
    {
        var syncMessage = hasChanges
            ? $"Synchronization completed via {trigger}."
            : $"Repository already up to date via {trigger}.";

        if (wasDirty)
        {
            syncMessage += " Local worktree changes were replaced by remote state.";
        }

        if (!string.IsNullOrWhiteSpace(refreshMessage))
        {
            syncMessage += $" {refreshMessage}";
        }

        return syncMessage;
    }

    private async Task PublishUpdatedEventAsync(
        GitRepositoryRegistration registration,
        string? previousCommit,
        string? currentCommit,
        bool hasChanges,
        GitSyncTrigger trigger,
        string? branch,
        CancellationToken cancellationToken)
    {
        try
        {
            await localEventBus.PublishAsync(new GitRepositoryUpdatedEvent(
                registration.Id,
                trigger,
                previousCommit,
                currentCommit,
                hasChanges,
                DateTimeOffset.UtcNow,
                GetBoundDocumentGroups(registration.Id),
                registration.Provider,
                branch), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish Git repository update event for '{RepositoryId}'", registration.Id);
        }
    }

    private async Task<string?> RefreshBoundDocumentGroupsAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var markdownService = serviceProvider.GetService<IMoMarkdownService>();
        if (markdownService is null)
        {
            return null;
        }

        var groups = GetBoundDocumentGroups(repositoryId);
        if (groups.Count == 0)
        {
            return null;
        }

        var errors = new List<string>();
        foreach (var groupKey in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await markdownService.RefreshAsync(groupKey);
            }
            catch (Exception ex)
            {
                errors.Add($"{groupKey}: {ex.Message}");
            }
        }

        return errors.Count == 0
            ? $"Refreshed {groups.Count} bound Markdown group(s)."
            : $"Markdown refresh warnings: {string.Join("; ", errors)}";
    }

    private static string? ResolveTargetBranchName(
        Repository repository,
        string remoteName,
        string? configuredBranch)
    {
        if (!string.IsNullOrWhiteSpace(configuredBranch))
        {
            return NormalizeBranchName(configuredBranch);
        }

        if (!repository.Info.IsHeadDetached && !string.IsNullOrWhiteSpace(repository.Head.FriendlyName))
        {
            return NormalizeBranchName(repository.Head.FriendlyName);
        }

        if (repository.Refs[$"refs/remotes/{remoteName}/HEAD"] is SymbolicReference remoteHead)
        {
            var prefix = $"refs/remotes/{remoteName}/";
            if (remoteHead.TargetIdentifier.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return remoteHead.TargetIdentifier[prefix.Length..];
            }
        }

        return repository.Branches
            .Where(x => x.IsRemote && x.FriendlyName.StartsWith($"{remoteName}/", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.FriendlyName[(remoteName.Length + 1)..])
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string NormalizeBranchName(string branchName)
    {
        const string headsPrefix = "refs/heads/";
        const string remotesPrefix = "refs/remotes/origin/";

        var normalized = branchName.Trim();
        if (normalized.StartsWith(headsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[headsPrefix.Length..];
        }

        if (normalized.StartsWith(remotesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized[remotesPrefix.Length..];
        }

        return normalized;
    }

    private static void FetchRemote(
        Repository repository,
        Remote remote,
        GitResolvedCredential? resolvedCredential)
    {
        var fetchOptions = new FetchOptions
        {
            CredentialsProvider = CreateCredentialsProvider(resolvedCredential)
        };

        Commands.Fetch(
            repository,
            remote.Name,
            remote.FetchRefSpecs.Select(x => x.Specification),
            fetchOptions,
            null);
    }

    private static LibGit2Sharp.Handlers.CredentialsHandler? CreateCredentialsProvider(GitResolvedCredential? resolvedCredential)
    {
        return resolvedCredential is null
            ? null
            : (_, _, _) => resolvedCredential.ToLibGitCredential();
    }
}
