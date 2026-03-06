using Monica.Markdown.Git.Models;

namespace Monica.Markdown.Git.Interfaces;

/// <summary>
/// Resolves a configured Git credential into transport credentials.
/// </summary>
public interface IGitCredentialResolver
{
    /// <summary>
    /// Gets the resolver identifier.
    /// </summary>
    string ResolverId { get; }

    /// <summary>
    /// Gets the resolver display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the resolver execution order. Higher values execute first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Attempts to resolve the credential.
    /// </summary>
    ValueTask<GitResolvedCredential?> ResolveAsync(
        GitCredentialResolutionContext context,
        CancellationToken cancellationToken = default);
}
