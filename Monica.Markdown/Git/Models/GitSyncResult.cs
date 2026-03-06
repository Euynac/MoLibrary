namespace Monica.Markdown.Git.Models;

/// <summary>
/// Represents the outcome of a repository synchronization attempt.
/// </summary>
public sealed class GitSyncResult
{
    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Gets the synchronization trigger.
    /// </summary>
    public required GitSyncTrigger Trigger { get; init; }

    /// <summary>
    /// Gets whether the synchronization succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets whether the commit changed.
    /// </summary>
    public required bool HasChanges { get; init; }

    /// <summary>
    /// Gets the commit SHA before synchronization.
    /// </summary>
    public string? PreviousCommit { get; init; }

    /// <summary>
    /// Gets the commit SHA after synchronization.
    /// </summary>
    public string? CurrentCommit { get; init; }

    /// <summary>
    /// Gets the result message.
    /// </summary>
    public required string Message { get; init; }
}
