namespace Monica.Markdown.Git.Models;

/// <summary>
/// Represents the outcome of deleting a local repository working copy.
/// </summary>
public sealed class GitRepositoryDeleteResult
{
    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Gets the resolved local repository path.
    /// </summary>
    public required string ResolvedLocalPath { get; init; }

    /// <summary>
    /// Gets whether the delete operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets whether the local path existed before deletion.
    /// </summary>
    public required bool PathExisted { get; init; }

    /// <summary>
    /// Gets the result message.
    /// </summary>
    public required string Message { get; init; }
}
