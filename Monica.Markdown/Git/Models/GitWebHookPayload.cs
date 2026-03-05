namespace Monica.Markdown.Git.Models;

/// <summary>
/// Normalized, provider-agnostic webhook payload for Git push events.
/// </summary>
public class GitWebHookPayload
{
    /// <summary>
    /// Remote URL of the repository that received the push.
    /// </summary>
    public required string RepositoryUrl { get; init; }

    /// <summary>
    /// Branch that was pushed to (short name, e.g. "main").
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>
    /// SHA of the head commit, if available.
    /// </summary>
    public string? CommitSha { get; init; }

    /// <summary>
    /// Message of the head commit, if available.
    /// </summary>
    public string? CommitMessage { get; init; }

    /// <summary>
    /// Username or display name of the person who triggered the push.
    /// </summary>
    public string? Sender { get; init; }
}
