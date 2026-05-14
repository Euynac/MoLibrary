using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.Sources.Internal;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Sources;

/// <summary>
/// EF Core-backed mutation group source.
/// </summary>
internal sealed class EfCoreMutationGroupSource(ConfigurationMutationGroupEfRepository repository)
    : IConfigurationMutationGroupSource
{
    /// <inheritdoc />
    public Task UpsertAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        return repository.UpsertAsync(group, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken)
    {
        return repository.GetAsync(groupId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        return repository.ListAsync(from, to, definitionKey, cancellationToken);
    }
}
