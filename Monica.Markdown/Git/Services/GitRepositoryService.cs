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
        var result = new List<GitRepositoryStatus>(_repositories.Count);

        foreach (var registration in _repositories.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(CreateSnapshot(registration));
        }

        return Task.FromResult<IReadOnlyList<GitRepositoryStatus>>(result);
    }

    /// <inheritdoc />
    public Task<GitRepositoryStatus> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var registration = GetRegistration(repositoryId);
        return Task.FromResult(CreateSnapshot(registration));
    }

    /// <inheritdoc />
    public Task<GitRepositoryStatus?> FindRepositoryByRemoteUrlAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
        if (!_option.IsSyncTriggerEnabled(trigger))
        {
            return CreateDisabledSyncResult(repositoryId, trigger);
        }

        var registration = GetRegistration(repositoryId);
        var state = _runtimeStates[registration.Id];
        var gate = GetLock(registration.Id);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var resolvedLocalPath = Path.GetFullPath(registration.LocalPath);
            var previousCommit = state.Capture().CurrentCommit;

            state.BeginSync(resolvedLocalPath);

            try
            {
                var resolvedCredential = await credentialManager.ResolveAsync(registration, cancellationToken);
                using var repository = OpenOrCloneRepository(
                    registration,
                    resolvedLocalPath,
                    resolvedCredential,
                    state,
                    cancellationToken);

                var remote = repository.Network.Remotes["origin"]
                             ?? repository.Network.Remotes.FirstOrDefault()
                             ?? throw new InvalidOperationException(
                                 $"Repository '{registration.Id}' does not contain a Git remote.");

                FetchRemote(repository, remote, resolvedCredential, state, resolvedLocalPath, cancellationToken);

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

                var checkoutOptions = new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force,
                    OnCheckoutProgress = (_, completedSteps, totalSteps) =>
                        UpdateCheckoutProgress(state, completedSteps, totalSteps, resolvedLocalPath)
                };

                Commands.Checkout(repository, localBranch, checkoutOptions);

                var wasDirty = repository.RetrieveStatus().IsDirty;
                repository.Reset(ResetMode.Hard, remoteBranch.Tip);
                repository.RemoveUntrackedFiles();

                state.UpdateOperationProgress(GitRepositoryProgressStages.RefreshingDocuments, null, resolvedLocalPath);
                var refreshMessage = await RefreshBoundDocumentGroupsAsync(registration.Id, cancellationToken);

                var currentCommit = repository.Head.Tip?.Sha;
                var hasChanges = !string.Equals(previousCommit, currentCommit, StringComparison.Ordinal);
                var syncMessage = BuildSyncMessage(trigger, hasChanges, wasDirty, refreshMessage);
                state.CompleteSync(repository, resolvedLocalPath, targetBranchName, syncMessage, DateTimeOffset.UtcNow);

                await PublishUpdatedEventAsync(
                    registration,
                    previousCommit,
                    currentCommit,
                    hasChanges,
                    trigger,
                    targetBranchName,
                    cancellationToken);

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
                    Message = syncMessage
                };
            }
            catch (Exception ex)
            {
                RefreshRuntimeStateFromDisk(state, resolvedLocalPath);
                state.MarkFailed(ex.Message);

                logger.LogError(ex, "Failed to synchronize Git repository '{RepositoryId}'", registration.Id);

                return new GitSyncResult
                {
                    RepositoryId = registration.Id,
                    Trigger = trigger,
                    Success = false,
                    HasChanges = false,
                    PreviousCommit = previousCommit,
                    CurrentCommit = state.Capture().CurrentCommit,
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
    public async Task<GitRepositoryDeleteResult> DeleteRepositoryAsync(
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        var registration = GetRegistration(repositoryId);
        var state = _runtimeStates[registration.Id];
        var gate = GetLock(registration.Id);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var resolvedLocalPath = Path.GetFullPath(registration.LocalPath);
            var pathExisted = Directory.Exists(resolvedLocalPath);

            try
            {
                if (File.Exists(resolvedLocalPath))
                {
                    throw new InvalidOperationException(
                        $"Local path '{resolvedLocalPath}' is a file and cannot be deleted as a repository directory.");
                }

                state.BeginDeletion();

                if (pathExisted)
                {
                    await DeleteDirectoryAsync(resolvedLocalPath, cancellationToken);
                }

                var refreshMessage = await RefreshBoundDocumentGroupsAsync(registration.Id, cancellationToken);
                var deleteMessage = BuildDeleteMessage(pathExisted, refreshMessage);

                state.CompleteDeletion(deleteMessage);

                logger.LogInformation(
                    "Deleted local Git repository working copy '{RepositoryId}' at '{LocalPath}'",
                    registration.Id,
                    resolvedLocalPath);

                return new GitRepositoryDeleteResult
                {
                    RepositoryId = registration.Id,
                    ResolvedLocalPath = resolvedLocalPath,
                    Success = true,
                    PathExisted = pathExisted,
                    Message = deleteMessage
                };
            }
            catch (Exception ex)
            {
                RefreshRuntimeStateFromDisk(state, resolvedLocalPath);
                state.MarkFailed(ex.Message);

                logger.LogError(ex, "Failed to delete local Git repository '{RepositoryId}'", registration.Id);

                return new GitRepositoryDeleteResult
                {
                    RepositoryId = registration.Id,
                    ResolvedLocalPath = resolvedLocalPath,
                    Success = false,
                    PathExisted = pathExisted,
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
        if (!_option.IsSyncTriggerEnabled(trigger))
        {
            return _repositories.Keys
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(repositoryId => CreateDisabledSyncResult(repositoryId, trigger))
                .ToList();
        }

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
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(credentialManager.GetCredentialInfos());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GitCredentialResolverInfo>> GetCredentialResolversAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    private static GitSyncResult CreateDisabledSyncResult(string repositoryId, GitSyncTrigger trigger)
    {
        return new GitSyncResult
        {
            RepositoryId = repositoryId,
            Trigger = trigger,
            Success = false,
            HasChanges = false,
            Message = BuildDisabledSyncMessage(trigger)
        };
    }

    private static string BuildDisabledSyncMessage(GitSyncTrigger trigger)
    {
        return $"Git synchronization triggered by '{trigger}' is disabled by module configuration.";
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

        RefreshRuntimeStateFromDisk(state, resolvedLocalPath);
        var snapshot = state.Capture();
        var displayState = ResolveDisplayState(snapshot.State, snapshot.IsAvailable);

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
            ResolvedBranch = snapshot.ResolvedBranch,
            CredentialId = registration.CredentialId,
            State = displayState,
            IsAvailable = snapshot.IsAvailable,
            IsDirty = snapshot.IsDirty,
            CurrentCommit = snapshot.CurrentCommit,
            ProgressStage = snapshot.ProgressStage,
            ProgressPercent = snapshot.ProgressPercent,
            WorkingDirectorySizeBytes = snapshot.WorkingDirectorySizeBytes,
            LastSyncAtUtc = snapshot.LastSyncAtUtc,
            LastSyncMessage = snapshot.LastSyncMessage,
            LastError = snapshot.LastError,
            BoundDocumentGroupKeys = boundGroups,
            MissingDocumentGroupKeys = missingGroups
        };
    }

    private static GitRepositorySyncState ResolveDisplayState(GitRepositorySyncState state, bool isAvailable)
    {
        if (state == GitRepositorySyncState.Syncing)
        {
            return GitRepositorySyncState.Syncing;
        }

        return isAvailable ? state : GitRepositorySyncState.NotReady;
    }

    private static void RefreshRuntimeStateFromDisk(GitRepositoryRuntimeState state, string resolvedLocalPath)
    {
        if (state.TryRefreshMetricsWhileSyncing(resolvedLocalPath))
        {
            return;
        }

        if (!Directory.Exists(resolvedLocalPath) || !Repository.IsValid(resolvedLocalPath))
        {
            state.MarkNotReadyFromDisk();

            return;
        }

        using var repository = new Repository(resolvedLocalPath);
        var isDirty = repository.RetrieveStatus().IsDirty;
        var currentCommit = repository.Head.Tip?.Sha;
        var resolvedBranch = repository.Info.IsHeadDetached
            ? null
            : NormalizeBranchName(repository.Head.FriendlyName);
        state.RefreshFromRepository(resolvedBranch, currentCommit, isDirty, resolvedLocalPath);
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
        GitResolvedCredential? resolvedCredential,
        GitRepositoryRuntimeState state,
        CancellationToken cancellationToken)
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
            state.UpdateOperationProgress(GitRepositoryProgressStages.Cloning, 0d, resolvedLocalPath);

            var cloneOptions = new CloneOptions
            {
                BranchName = registration.Branch,
                OnCheckoutProgress = (_, completedSteps, totalSteps) =>
                    UpdateCheckoutProgress(state, completedSteps, totalSteps, resolvedLocalPath)
            };

            cloneOptions.FetchOptions.CredentialsProvider = CreateCredentialsProvider(resolvedCredential);
            cloneOptions.FetchOptions.OnTransferProgress = progress =>
                UpdateTransferProgress(state, GitRepositoryProgressStages.Cloning, progress, resolvedLocalPath, cancellationToken);

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

    private static string BuildDeleteMessage(bool pathExisted, string? refreshMessage)
    {
        var message = pathExisted
            ? "Local repository deleted. Synchronize to clone again."
            : "Local repository path was already missing. Synchronize to clone again.";

        if (!string.IsNullOrWhiteSpace(refreshMessage))
        {
            message += $" {refreshMessage}";
        }

        return message;
    }

    private static async Task DeleteDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return;
                }

                DeleteDirectoryCore(directoryPath);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryableDeleteException(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }

        if (Directory.Exists(directoryPath))
        {
            DeleteDirectoryCore(directoryPath);
        }
    }

    private static void DeleteDirectoryCore(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);
        if (!directoryInfo.Exists)
        {
            return;
        }

        foreach (var fileInfo in directoryInfo.EnumerateFiles())
        {
            fileInfo.Attributes = FileAttributes.Normal;
            fileInfo.Delete();
        }

        foreach (var subdirectoryInfo in directoryInfo.EnumerateDirectories())
        {
            if (subdirectoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                subdirectoryInfo.Attributes = FileAttributes.Normal;
                subdirectoryInfo.Delete();
                continue;
            }

            DeleteDirectoryCore(subdirectoryInfo.FullName);
        }

        directoryInfo.Attributes = FileAttributes.Normal;
        directoryInfo.Delete();
    }

    private static bool IsRetryableDeleteException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
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
        GitResolvedCredential? resolvedCredential,
        GitRepositoryRuntimeState state,
        string resolvedLocalPath,
        CancellationToken cancellationToken)
    {
        state.UpdateOperationProgress(GitRepositoryProgressStages.Fetching, 0d, resolvedLocalPath);

        var fetchOptions = new FetchOptions
        {
            CredentialsProvider = CreateCredentialsProvider(resolvedCredential),
            OnTransferProgress = progress =>
                UpdateTransferProgress(state, GitRepositoryProgressStages.Fetching, progress, resolvedLocalPath, cancellationToken)
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

    private static bool UpdateTransferProgress(
        GitRepositoryRuntimeState state,
        string progressStage,
        TransferProgress progress,
        string resolvedLocalPath,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var completedObjects = Math.Max(progress.ReceivedObjects, progress.IndexedObjects);
        double? progressPercent = progress.TotalObjects > 0
            ? Math.Clamp(completedObjects * 100d / progress.TotalObjects, 0d, 100d)
            : null;

        state.UpdateOperationProgress(progressStage, progressPercent, resolvedLocalPath);
        return true;
    }

    private static void UpdateCheckoutProgress(
        GitRepositoryRuntimeState state,
        int completedSteps,
        int totalSteps,
        string resolvedLocalPath)
    {
        double? progressPercent = totalSteps > 0
            ? Math.Clamp(completedSteps * 100d / totalSteps, 0d, 100d)
            : null;

        state.UpdateOperationProgress(GitRepositoryProgressStages.Checkout, progressPercent, resolvedLocalPath);
    }
}
