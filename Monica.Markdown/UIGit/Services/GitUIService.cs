using Microsoft.Extensions.Logging;
using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;
using Monica.Tool.MoResponse;

namespace Monica.Markdown.UIGit.Services;

/// <summary>
/// UI service wrapper for Git operations.
/// </summary>
public class GitUIService(IGitRepositoryService repositoryService, ILogger<GitUIService> logger)
{
    /// <summary>
    /// Gets all repository snapshots.
    /// </summary>
    public async Task<Res<IReadOnlyList<GitRepositoryStatus>>> GetRepositoriesAsync()
    {
        try
        {
            return Res.Ok(await repositoryService.GetRepositoriesAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Git repositories");
            return Res.Fail($"Failed to load Git repositories: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a single repository snapshot.
    /// </summary>
    public async Task<Res<GitRepositoryStatus>> GetRepositoryAsync(string repositoryId)
    {
        try
        {
            return Res.Ok(await repositoryService.GetRepositoryAsync(repositoryId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Git repository {RepositoryId}", repositoryId);
            return Res.Fail($"Failed to load Git repository: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets safe credential metadata.
    /// </summary>
    public async Task<Res<IReadOnlyList<GitCredentialInfo>>> GetCredentialsAsync()
    {
        try
        {
            return Res.Ok(await repositoryService.GetCredentialsAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Git credentials");
            return Res.Fail($"Failed to load Git credentials: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets registered credential resolver metadata.
    /// </summary>
    public async Task<Res<IReadOnlyList<GitCredentialResolverInfo>>> GetCredentialResolversAsync()
    {
        try
        {
            return Res.Ok(await repositoryService.GetCredentialResolversAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Git credential resolvers");
            return Res.Fail($"Failed to load Git credential resolvers: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronizes a repository.
    /// </summary>
    public async Task<Res<GitSyncResult>> SyncRepositoryAsync(string repositoryId)
    {
        try
        {
            return Res.Ok(await repositoryService.SyncRepositoryAsync(repositoryId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize Git repository {RepositoryId}", repositoryId);
            return Res.Fail($"Failed to synchronize Git repository: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronizes all repositories.
    /// </summary>
    public async Task<Res<IReadOnlyList<GitSyncResult>>> SyncAllAsync()
    {
        try
        {
            return Res.Ok(await repositoryService.SyncAllAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize all Git repositories");
            return Res.Fail($"Failed to synchronize all Git repositories: {ex.Message}");
        }
    }
}
