using Microsoft.Extensions.Logging;
using Monica.Markdown.Git.Abstractions;
using Monica.Markdown.Git.Models;
using Monica.Tool.MoResponse;

namespace Monica.Markdown.UIGit.Services;

/// <summary>
/// UI service wrapping Git infrastructure services with <see cref="Res{T}"/> return types
/// for consumption by Blazor components.
/// </summary>
public class GitUIService(
    IGitRepositoryService repositoryService,
    IGitCredentialStore credentialStore,
    ILogger<GitUIService> logger)
{
    /// <summary>
    /// Gets runtime information for all registered repositories.
    /// </summary>
    public Task<Res<IReadOnlyList<GitRepositoryInfo>>> GetAllRepositoriesAsync()
    {
        try
        {
            var repos = repositoryService.GetAllRepositories();
            return Task.FromResult(Res.Ok(repos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load repositories");
            return Task.FromResult(Res.Fail<IReadOnlyList<GitRepositoryInfo>>($"Failed to load repositories: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets runtime information for a specific repository.
    /// </summary>
    public Task<Res<GitRepositoryInfo>> GetRepositoryInfoAsync(string repositoryId)
    {
        try
        {
            var info = repositoryService.GetRepositoryInfo(repositoryId);
            return Task.FromResult(Res.Ok(info));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load repository info: {RepositoryId}", repositoryId);
            return Task.FromResult(Res.Fail<GitRepositoryInfo>($"Failed to load repository: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets all credentials with sensitive fields masked.
    /// </summary>
    public Task<Res<IReadOnlyList<GitCredential>>> GetAllCredentialsAsync()
    {
        try
        {
            var credentials = credentialStore.GetAllCredentials()
                .Select(c => new GitCredential
                {
                    Id = c.Id,
                    DisplayName = c.DisplayName,
                    Type = c.Type,
                    Token = MaskSensitive(c.Token),
                    Username = c.Username,
                    Password = MaskSensitive(c.Password)
                })
                .ToList();

            return Task.FromResult(Res.Ok<IReadOnlyList<GitCredential>>(credentials));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load credentials");
            return Task.FromResult(Res.Fail<IReadOnlyList<GitCredential>>($"Failed to load credentials: {ex.Message}"));
        }
    }

    /// <summary>
    /// Triggers a pull operation for the specified repository.
    /// </summary>
    public async Task<Res> PullRepositoryAsync(string repositoryId)
    {
        try
        {
            await repositoryService.PullAsync(repositoryId);
            return Res.Ok("Repository pulled successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pull repository: {RepositoryId}", repositoryId);
            return Res.Fail($"Failed to pull repository: {ex.Message}");
        }
    }

    private static string? MaskSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Length <= 4)
            return "****";

        return string.Concat(value.AsSpan(0, 4), "****");
    }
}
