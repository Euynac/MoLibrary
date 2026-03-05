using System.Collections.Concurrent;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.Markdown.Git.Abstractions;
using Monica.Markdown.Git.Events;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Git.WebHooks;
using Monica.Markdown.Modules;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// Core Git repository service. Manages clone/pull operations, runtime state tracking,
/// and webhook handling via LibGit2Sharp.
/// </summary>
public class GitRepositoryService : IGitRepositoryService
{
    private readonly ILogger<GitRepositoryService> _logger;
    private readonly IGitCredentialStore _credentialStore;
    private readonly IMoLocalEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ModuleGitOption _option;
    private readonly ConcurrentDictionary<string, GitRepositoryInfo> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitRepositoryRegistration> _registrations;

    public GitRepositoryService(
        IOptions<ModuleGitOption> options,
        IGitCredentialStore credentialStore,
        IMoLocalEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<GitRepositoryService> logger)
    {
        _logger = logger;
        _credentialStore = credentialStore;
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _option = options.Value;
        _registrations = _option.Repositories.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

        // Initialize runtime info from registrations
        foreach (var reg in _option.Repositories)
        {
            _repositories[reg.Id] = new GitRepositoryInfo
            {
                Id = reg.Id,
                Url = reg.Url,
                LocalPath = reg.LocalPath,
                Branch = reg.Branch,
                Status = Directory.Exists(Path.Combine(reg.LocalPath, ".git"))
                    ? EGitRepositoryStatus.Ready
                    : EGitRepositoryStatus.Unknown
            };
        }
    }

    /// <inheritdoc />
    public async Task CloneAsync(string repositoryId, CancellationToken ct = default)
    {
        var registration = GetRegistration(repositoryId);
        var info = GetOrCreateInfo(registration);

        if (Directory.Exists(Path.Combine(registration.LocalPath, ".git")))
            throw new InvalidOperationException($"Repository '{repositoryId}' already exists at '{registration.LocalPath}'.");

        info.Status = EGitRepositoryStatus.Cloning;
        info.ErrorMessage = null;

        try
        {
            _logger.LogInformation("Cloning repository '{RepositoryId}' from {Url} to {Path}",
                repositoryId, registration.Url, registration.LocalPath);

            var cloneOptions = new CloneOptions
            {
                BranchName = registration.Branch,
                CredentialsProvider = BuildCredentialsHandler(registration.CredentialId)
            };

            await Task.Run(() => Repository.Clone(registration.Url, registration.LocalPath, cloneOptions), ct);

            UpdateInfoFromLocalRepo(info, registration.LocalPath);
            info.Status = EGitRepositoryStatus.Ready;
            info.LastSyncUtc = DateTime.UtcNow;

            _logger.LogInformation("Successfully cloned repository '{RepositoryId}'", repositoryId);
        }
        catch (Exception ex)
        {
            info.Status = EGitRepositoryStatus.Error;
            info.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to clone repository '{RepositoryId}'", repositoryId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PullAsync(string repositoryId, CancellationToken ct = default)
    {
        var registration = GetRegistration(repositoryId);
        var info = GetOrCreateInfo(registration);

        info.Status = EGitRepositoryStatus.Pulling;
        info.ErrorMessage = null;

        try
        {
            _logger.LogInformation("Pulling repository '{RepositoryId}'", repositoryId);

            await Task.Run(() =>
            {
                using var repo = new Repository(registration.LocalPath);
                var signature = new Signature("Monica", "monica@localhost", DateTimeOffset.UtcNow);
                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = BuildCredentialsHandler(registration.CredentialId)
                    }
                };

                Commands.Pull(repo, signature, pullOptions);
            }, ct);

            UpdateInfoFromLocalRepo(info, registration.LocalPath);
            info.Status = EGitRepositoryStatus.Ready;
            info.LastSyncUtc = DateTime.UtcNow;

            _logger.LogInformation("Successfully pulled repository '{RepositoryId}'", repositoryId);

            await _eventBus.PublishAsync(new GitRepositoryUpdatedEvent
            {
                RepositoryId = repositoryId,
                Branch = info.Branch ?? "unknown",
                CommitSha = info.LastCommitSha,
                Source = EGitUpdateSource.ManualPull
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            info.Status = EGitRepositoryStatus.Error;
            info.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to pull repository '{RepositoryId}'", repositoryId);
            throw;
        }
    }

    /// <inheritdoc />
    public GitRepositoryInfo GetRepositoryInfo(string repositoryId)
    {
        if (!_repositories.TryGetValue(repositoryId, out var info))
            throw new KeyNotFoundException($"Git repository '{repositoryId}' not found.");

        return info;
    }

    /// <inheritdoc />
    public IReadOnlyList<GitRepositoryInfo> GetAllRepositories()
    {
        return _repositories.Values.ToList();
    }

    /// <inheritdoc />
    public async Task HandleWebHookAsync(string providerKey, HttpRequest request, CancellationToken ct = default)
    {
        var provider = _serviceProvider.GetKeyedService<IGitWebHookProvider>(providerKey)
            ?? throw new KeyNotFoundException($"Webhook provider '{providerKey}' not found.");

        if (!provider.ValidateRequest(request))
        {
            _logger.LogWarning("Webhook request validation failed for provider '{Provider}'", providerKey);
            throw new InvalidOperationException($"Webhook validation failed for provider '{providerKey}'.");
        }

        var payload = await provider.ParsePayloadAsync(request);
        if (payload is null)
        {
            _logger.LogDebug("Webhook event from provider '{Provider}' was not a relevant push event", providerKey);
            return;
        }

        // Match payload to a registered repository by URL
        var matchedRegistration = _option.Repositories.FirstOrDefault(r =>
            IsUrlMatch(r.Url, payload.RepositoryUrl) &&
            (r.Branch is null || string.Equals(r.Branch, payload.Branch, StringComparison.OrdinalIgnoreCase)));

        if (matchedRegistration is null)
        {
            _logger.LogDebug("No registered repository matches webhook payload from {Url} branch {Branch}",
                payload.RepositoryUrl, payload.Branch);
            return;
        }

        _logger.LogInformation("Webhook matched repository '{RepositoryId}', pulling changes", matchedRegistration.Id);

        var info = GetOrCreateInfo(matchedRegistration);
        info.Status = EGitRepositoryStatus.Pulling;
        info.ErrorMessage = null;

        try
        {
            await Task.Run(() =>
            {
                using var repo = new Repository(matchedRegistration.LocalPath);
                var signature = new Signature("Monica", "monica@localhost", DateTimeOffset.UtcNow);
                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = BuildCredentialsHandler(matchedRegistration.CredentialId)
                    }
                };

                Commands.Pull(repo, signature, pullOptions);
            }, ct);

            UpdateInfoFromLocalRepo(info, matchedRegistration.LocalPath);
            info.Status = EGitRepositoryStatus.Ready;
            info.LastSyncUtc = DateTime.UtcNow;

            await _eventBus.PublishAsync(new GitRepositoryUpdatedEvent
            {
                RepositoryId = matchedRegistration.Id,
                Branch = payload.Branch,
                CommitSha = payload.CommitSha ?? info.LastCommitSha,
                CommitMessage = payload.CommitMessage,
                Source = EGitUpdateSource.WebHook
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            info.Status = EGitRepositoryStatus.Error;
            info.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to pull repository '{RepositoryId}' via webhook", matchedRegistration.Id);
            throw;
        }
    }

    private GitRepositoryRegistration GetRegistration(string repositoryId)
    {
        if (!_registrations.TryGetValue(repositoryId, out var registration))
            throw new KeyNotFoundException($"Git repository registration '{repositoryId}' not found.");

        return registration;
    }

    private GitRepositoryInfo GetOrCreateInfo(GitRepositoryRegistration registration)
    {
        return _repositories.GetOrAdd(registration.Id, _ => new GitRepositoryInfo
        {
            Id = registration.Id,
            Url = registration.Url,
            LocalPath = registration.LocalPath,
            Branch = registration.Branch
        });
    }

    private CredentialsHandler? BuildCredentialsHandler(string? credentialId)
    {
        if (string.IsNullOrEmpty(credentialId))
            return null;

        var credential = _credentialStore.GetCredential(credentialId);

        return credential.Type switch
        {
            EGitCredentialType.Token => (_, _, _) =>
                new UsernamePasswordCredentials { Username = credential.Token, Password = string.Empty },
            EGitCredentialType.UsernamePassword => (_, _, _) =>
                new UsernamePasswordCredentials { Username = credential.Username, Password = credential.Password },
            _ => null
        };
    }

    private static void UpdateInfoFromLocalRepo(GitRepositoryInfo info, string localPath)
    {
        try
        {
            using var repo = new Repository(localPath);
            info.Branch = repo.Head.FriendlyName;
            info.LastCommitSha = repo.Head.Tip?.Sha;
        }
        catch
        {
            // Non-critical — info update failure should not block operations
        }
    }

    private static bool IsUrlMatch(string registeredUrl, string payloadUrl)
    {
        // Normalize URLs for comparison: remove trailing .git and compare case-insensitively
        static string Normalize(string url) =>
            url.TrimEnd('/').TrimEnd(".git".ToCharArray()).ToLowerInvariant();

        return string.Equals(Normalize(registeredUrl), Normalize(payloadUrl), StringComparison.Ordinal);
    }
}
