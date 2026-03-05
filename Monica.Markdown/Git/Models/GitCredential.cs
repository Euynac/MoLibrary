namespace Monica.Markdown.Git.Models;

/// <summary>
/// Represents stored Git authentication credentials.
/// </summary>
public class GitCredential
{
    /// <summary>
    /// Unique identifier for this credential entry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Optional human-readable name for display purposes.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Authentication type (Token or UsernamePassword).
    /// </summary>
    public required EGitCredentialType Type { get; init; }

    /// <summary>
    /// Personal access token. Used when <see cref="Type"/> is <see cref="EGitCredentialType.Token"/>.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Username. Used when <see cref="Type"/> is <see cref="EGitCredentialType.UsernamePassword"/>.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Password. Used when <see cref="Type"/> is <see cref="EGitCredentialType.UsernamePassword"/>.
    /// </summary>
    public string? Password { get; init; }
}
