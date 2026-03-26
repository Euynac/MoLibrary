namespace Monica.DevOps.Git.Models;

/// <summary>
/// Safe credential metadata returned to UI callers.
/// </summary>
public sealed class GitCredentialInfo
{
    /// <summary>
    /// Gets the credential identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the authentication type.
    /// </summary>
    public required GitAuthenticationType AuthenticationType { get; init; }

    /// <summary>
    /// Gets the optional user name.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets whether a secret value is present.
    /// </summary>
    public required bool HasSecret { get; init; }
}
