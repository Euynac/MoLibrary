using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Abstractions;

/// <summary>
/// Provides access to registered Git credentials.
/// Throws <see cref="KeyNotFoundException"/> when a credential is not found.
/// </summary>
public interface IGitCredentialStore
{
    /// <summary>
    /// Gets a credential by its unique identifier.
    /// </summary>
    /// <param name="credentialId">The credential identifier.</param>
    /// <returns>The matching credential.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the credential ID is not found.</exception>
    GitCredential GetCredential(string credentialId);

    /// <summary>
    /// Gets all registered credentials.
    /// </summary>
    IReadOnlyList<GitCredential> GetAllCredentials();
}
