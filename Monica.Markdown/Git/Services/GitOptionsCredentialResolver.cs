using Monica.Markdown.Git.Interfaces;
using Monica.Markdown.Git.Models;
using Monica.Modules;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// Resolves credentials directly from <see cref="ModuleGitOption"/> registrations.
/// </summary>
public sealed class GitOptionsCredentialResolver : IGitCredentialResolver
{
    /// <inheritdoc />
    public string ResolverId => "options";

    /// <inheritdoc />
    public string DisplayName => "ModuleGit option registry";

    /// <inheritdoc />
    public int Order => 100;

    /// <inheritdoc />
    public ValueTask<GitResolvedCredential?> ResolveAsync(
        GitCredentialResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        var registration = context.Registration;

        GitResolvedCredential? resolved = registration.AuthenticationType switch
        {
            GitAuthenticationType.Token when !string.IsNullOrWhiteSpace(registration.Token)
                => new GitResolvedCredential(
                    registration.Id,
                    registration.UserName ?? "git",
                    registration.Token,
                    registration.AuthenticationType),
            GitAuthenticationType.UserPassword
                when !string.IsNullOrWhiteSpace(registration.UserName)
                     && !string.IsNullOrWhiteSpace(registration.Password)
                => new GitResolvedCredential(
                    registration.Id,
                    registration.UserName,
                    registration.Password,
                    registration.AuthenticationType),
            _ => null
        };

        return ValueTask.FromResult(resolved);
    }
}
