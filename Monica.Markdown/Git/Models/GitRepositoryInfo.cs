namespace Monica.Markdown.Git.Models;

/// <summary>
/// Runtime state of a tracked Git repository.
/// </summary>
public class GitRepositoryInfo
{
    /// <summary>
    /// Unique identifier matching the <see cref="GitRepositoryRegistration.Id"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Remote URL of the repository.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Local filesystem path of the cloned repository.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Currently tracked branch name.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>
    /// SHA of the last known commit on the tracked branch.
    /// </summary>
    public string? LastCommitSha { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful sync operation.
    /// </summary>
    public DateTime? LastSyncUtc { get; set; }

    /// <summary>
    /// Current operational status of the repository.
    /// </summary>
    public EGitRepositoryStatus Status { get; set; } = EGitRepositoryStatus.Unknown;

    /// <summary>
    /// Error message from the last failed operation, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
