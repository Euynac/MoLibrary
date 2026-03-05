using Microsoft.AspNetCore.Http;
using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Abstractions;

/// <summary>
/// Provides access to Git repository operations including clone, pull, and webhook handling.
/// Throws exceptions on failure (infrastructure module — no Res wrapping).
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Clones a registered repository to its configured local path.
    /// </summary>
    /// <param name="repositoryId">The repository registration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the repository ID is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the repository already exists locally.</exception>
    Task CloneAsync(string repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Pulls the latest changes for a registered repository.
    /// </summary>
    /// <param name="repositoryId">The repository registration identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the repository ID is not found.</exception>
    Task PullAsync(string repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Gets the runtime state of a registered repository.
    /// </summary>
    /// <param name="repositoryId">The repository registration identifier.</param>
    /// <returns>Current runtime information for the repository.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the repository ID is not found.</exception>
    GitRepositoryInfo GetRepositoryInfo(string repositoryId);

    /// <summary>
    /// Gets runtime state for all registered repositories.
    /// </summary>
    IReadOnlyList<GitRepositoryInfo> GetAllRepositories();

    /// <summary>
    /// Handles an incoming webhook request from a Git hosting provider.
    /// Matches the payload to a registered repository and performs a pull.
    /// </summary>
    /// <param name="providerKey">Provider identifier (e.g. "github", "gitlab").</param>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HandleWebHookAsync(string providerKey, HttpRequest request, CancellationToken ct = default);
}
