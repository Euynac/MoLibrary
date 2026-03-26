using Microsoft.Extensions.Options;
using Monica.DevOps.Git.Abstractions;
using Monica.DevOps.Git.Models;
using Monica.Modules;

namespace Monica.DevOps.Git.Services;

/// <summary>
/// Coordinates credential lookup and resolution.
/// </summary>
public sealed class GitCredentialManager(
    IOptions<ModuleGitOption> options,
    IEnumerable<IGitCredentialResolver> resolvers)
{
    private readonly ModuleGitOption _option = options.Value;
    private readonly List<IGitCredentialResolver> _orderedResolvers = resolvers
        .OrderByDescending(x => x.Order)
        .ThenBy(x => x.ResolverId, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>
    /// Resolves a repository credential.
    /// </summary>
    public async Task<GitResolvedCredential?> ResolveAsync(
        GitRepositoryRegistration repository,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repository.CredentialId))
        {
            return null;
        }

        var registration = _option.CredentialRegistrations.FirstOrDefault(x =>
            string.Equals(x.Id, repository.CredentialId, StringComparison.OrdinalIgnoreCase));

        if (registration is null)
        {
            throw new KeyNotFoundException(
                $"Git credential '{repository.CredentialId}' is not registered.");
        }

        var context = new GitCredentialResolutionContext(registration, repository);

        foreach (var resolver in _orderedResolvers)
        {
            var resolved = await resolver.ResolveAsync(context, cancellationToken);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        throw new InvalidOperationException(
            $"No Git credential resolver could resolve credential '{registration.Id}'.");
    }

    /// <summary>
    /// Gets safe credential metadata.
    /// </summary>
    public IReadOnlyList<GitCredentialInfo> GetCredentialInfos()
    {
        return _option.CredentialRegistrations
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => new GitCredentialInfo
            {
                Id = x.Id,
                AuthenticationType = x.AuthenticationType,
                UserName = x.UserName,
                HasSecret = !string.IsNullOrWhiteSpace(x.Token) || !string.IsNullOrWhiteSpace(x.Password)
            })
            .ToArray();
    }

    /// <summary>
    /// Gets registered resolver metadata.
    /// </summary>
    public IReadOnlyList<GitCredentialResolverInfo> GetResolverInfos()
    {
        return _orderedResolvers
            .Select(x => new GitCredentialResolverInfo
            {
                ResolverId = x.ResolverId,
                DisplayName = x.DisplayName,
                Order = x.Order,
                ImplementationType = x.GetType().FullName ?? x.GetType().Name
            })
            .ToArray();
    }
}
