using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Interfaces;

/// <summary>
/// Provides repository inspection and synchronization operations.
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Gets all configured repositories with runtime state.
    /// </summary>
    Task<IReadOnlyList<GitRepositoryStatus>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single repository status.
    /// </summary>
    Task<GitRepositoryStatus> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a configured repository by remote URL.
    /// </summary>
    Task<GitRepositoryStatus?> FindRepositoryByRemoteUrlAsync(string remoteUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a configured repository.
    /// </summary>
    Task<GitSyncResult> SyncRepositoryAsync(
        string repositoryId,
        GitSyncTrigger trigger = GitSyncTrigger.Manual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all configured repositories.
    /// </summary>
    Task<IReadOnlyList<GitSyncResult>> SyncAllAsync(
        GitSyncTrigger trigger = GitSyncTrigger.Manual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configured credential metadata.
    /// </summary>
    Task<IReadOnlyList<GitCredentialInfo>> GetCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets registered credential resolver metadata.
    /// </summary>
    Task<IReadOnlyList<GitCredentialResolverInfo>> GetCredentialResolversAsync(CancellationToken cancellationToken = default);
}
