namespace Monica.Markdown.Git.Models;

/// <summary>
/// Declarative Git repository configuration registered during module setup.
/// </summary>
public class GitRepositoryRegistration
{
    /// <summary>
    /// Gets the unique repository identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the remote Git repository URL.
    /// </summary>
    public required string RemoteUrl { get; init; }

    /// <summary>
    /// Gets the local repository path.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Gets the optional configured branch.
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// Gets the optional credential identifier.
    /// </summary>
    public string? CredentialId { get; init; }

    /// <summary>
    /// Gets the optional provider label for display purposes.
    /// </summary>
    public string? Provider { get; init; }
}
