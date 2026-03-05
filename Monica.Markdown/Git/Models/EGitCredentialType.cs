namespace Monica.Markdown.Git.Models;

/// <summary>
/// Supported Git credential authentication types.
/// </summary>
public enum EGitCredentialType
{
    /// <summary>
    /// Personal access token authentication.
    /// </summary>
    Token,

    /// <summary>
    /// Username and password authentication.
    /// </summary>
    UsernamePassword
}
