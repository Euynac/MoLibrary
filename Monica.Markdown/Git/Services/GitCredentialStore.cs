using Microsoft.Extensions.Options;
using Monica.Markdown.Git.Abstractions;
using Monica.Markdown.Git.Models;
using Monica.Markdown.Modules;

namespace Monica.Markdown.Git.Services;

/// <summary>
/// In-memory credential store backed by <see cref="ModuleGitOption"/>.
/// </summary>
public class GitCredentialStore(IOptions<ModuleGitOption> options) : IGitCredentialStore
{
    private readonly Dictionary<string, GitCredential> _credentials =
        options.Value.Credentials.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public GitCredential GetCredential(string credentialId)
    {
        if (!_credentials.TryGetValue(credentialId, out var credential))
            throw new KeyNotFoundException($"Git credential '{credentialId}' not found.");

        return credential;
    }

    /// <inheritdoc />
    public IReadOnlyList<GitCredential> GetAllCredentials()
    {
        return _credentials.Values.ToList();
    }
}
