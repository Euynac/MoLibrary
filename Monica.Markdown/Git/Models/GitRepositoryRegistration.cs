namespace Monica.Markdown.Git.Models;

/// <summary>
/// Registration-time descriptor for a Git repository.
/// Configured via the module Guide at startup.
/// </summary>
public class GitRepositoryRegistration
{
    /// <summary>
    /// Unique identifier for this repository registration.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Remote URL of the Git repository.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Local filesystem path where the repository is cloned.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Optional credential ID referencing a <see cref="GitCredential"/>.
    /// </summary>
    public string? CredentialId { get; init; }

    /// <summary>
    /// Branch to track. Defaults to the remote default branch if null.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Whether to automatically clone the repository on startup if it does not exist locally.
    /// </summary>
    public bool AutoClone { get; set; } = true;
}
