using LibGit2Sharp;

namespace Monica.DevOps.Git.Models;

/// <summary>
/// Fully resolved Git credential ready for transport-level authentication.
/// </summary>
/// <param name="CredentialId">Credential identifier.</param>
/// <param name="UserName">Resolved user name.</param>
/// <param name="Secret">Resolved secret.</param>
/// <param name="AuthenticationType">Authentication type.</param>
public sealed record GitResolvedCredential(
    string CredentialId,
    string UserName,
    string Secret,
    GitAuthenticationType AuthenticationType)
{
    /// <summary>
    /// Creates the LibGit2Sharp credential object.
    /// </summary>
    public Credentials ToLibGitCredential()
    {
        return new UsernamePasswordCredentials
        {
            Username = UserName,
            Password = Secret
        };
    }
}
