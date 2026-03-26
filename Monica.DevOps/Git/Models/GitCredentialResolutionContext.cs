namespace Monica.DevOps.Git.Models;

/// <summary>
/// Provides context for credential resolution.
/// </summary>
/// <param name="Registration">Registered credential definition.</param>
/// <param name="Repository">Repository requesting the credential.</param>
public sealed record GitCredentialResolutionContext(
    GitCredentialRegistration Registration,
    GitRepositoryRegistration Repository);
