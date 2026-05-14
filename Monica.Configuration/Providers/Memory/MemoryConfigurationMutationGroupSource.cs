using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Providers.Memory;

/// <summary>
/// In-memory mutation group store used as the default writable implementation.
/// </summary>
public sealed class MemoryConfigurationMutationGroupSource : IConfigurationMutationGroupSource
{
    private readonly ConcurrentDictionary<string, ConfigurationMutationGroup> _groups = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task UpsertAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        _groups[group.GroupId] = group;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken)
    {
        _groups.TryGetValue(groupId, out var group);
        return Task.FromResult(group);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        IEnumerable<ConfigurationMutationGroup> query = _groups.Values;

        if (from is not null)
        {
            query = query.Where(group => group.CreatedTime >= from);
        }

        if (to is not null)
        {
            query = query.Where(group => group.CreatedTime <= to);
        }

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            query = query.Where(group =>
                group.DefinitionKeys.Any(key => string.Equals(key, definitionKey, StringComparison.OrdinalIgnoreCase)));
        }

        return Task.FromResult<IReadOnlyList<ConfigurationMutationGroup>>(
            query.OrderByDescending(group => group.CreatedTime).ToArray());
    }
}
