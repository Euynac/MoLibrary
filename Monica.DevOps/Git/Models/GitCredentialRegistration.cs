namespace Monica.DevOps.Git.Models;

/// <summary>
/// Declarative Git credential configuration registered during module setup.
/// </summary>
public class GitCredentialRegistration
{
    /// <summary>
    /// Gets the unique credential identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the authentication type used by this credential.
    /// </summary>
    public required GitAuthenticationType AuthenticationType { get; init; }

    /// <summary>
    /// Gets the optional user name.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets the token secret when <see cref="AuthenticationType"/> is <see cref="GitAuthenticationType.Token"/>.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the password secret when <see cref="AuthenticationType"/> is <see cref="GitAuthenticationType.UserPassword"/>.
    /// </summary>
    public string? Password { get; init; }
}
