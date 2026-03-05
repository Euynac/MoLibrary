namespace Monica.Markdown.Git.Events;

/// <summary>
/// Event published via <c>IMoLocalEventBus</c> when a Git repository is updated.
/// </summary>
public class GitRepositoryUpdatedEvent
{
    /// <summary>
    /// Identifier of the repository that was updated.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Branch that was updated.
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>
    /// SHA of the head commit after the update.
    /// </summary>
    public string? CommitSha { get; init; }

    /// <summary>
    /// Message of the head commit after the update.
    /// </summary>
    public string? CommitMessage { get; init; }

    /// <summary>
    /// Source that triggered the repository update.
    /// </summary>
    public required EGitUpdateSource Source { get; init; }
}

/// <summary>
/// Describes the source that triggered a Git repository update.
/// </summary>
public enum EGitUpdateSource
{
    /// <summary>
    /// Triggered by an incoming webhook from a Git hosting provider.
    /// </summary>
    WebHook,

    /// <summary>
    /// Triggered by a manual pull request from a user or API call.
    /// </summary>
    ManualPull,

    /// <summary>
    /// Triggered by an automatic sync background process.
    /// </summary>
    AutoSync
}
