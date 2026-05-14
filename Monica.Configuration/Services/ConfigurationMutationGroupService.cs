using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Default application-level service for persisted mutation groups.
/// </summary>
internal sealed class ConfigurationMutationGroupService(
    IEnumerable<IConfigurationMutationGroupSource> groupSources,
    IConfigurationHistoryService historyService)
    : IConfigurationMutationGroupService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup> BeginAsync(
        string label,
        string? reason,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var source = ResolveWritableSource();
        var now = DateTimeOffset.UtcNow;
        var group = new ConfigurationMutationGroup
        {
            GroupId = Guid.NewGuid().ToString("N"),
            Label = string.IsNullOrWhiteSpace(label) ? $"Changes {now:yyyy-MM-dd HH:mm}" : label.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedTime = now,
            ModifierId = context.ModifierId,
            ModifierName = context.ModifierName,
            Status = ConfigurationMutationGroupStatus.Applied
        };

        await source.UpsertAsync(group, cancellationToken);
        return group;
    }

    /// <inheritdoc />
    public Task CompleteAsync(
        string groupId,
        int mutationCount,
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(groupId, group => group with
        {
            MutationCount = mutationCount,
            DefinitionKeys = NormalizeDefinitionKeys(definitionKeys),
            Status = ConfigurationMutationGroupStatus.Applied
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkPartialAsync(
        string groupId,
        int successfulCount,
        IReadOnlyList<string> definitionKeys,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(groupId, group => group with
        {
            MutationCount = successfulCount,
            DefinitionKeys = NormalizeDefinitionKeys(definitionKeys),
            Status = ConfigurationMutationGroupStatus.PartiallyApplied
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkRolledBackAsync(
        string groupId,
        string rollbackGroupId,
        DateTimeOffset rolledBackTime,
        CancellationToken cancellationToken)
    {
        return UpdateAsync(groupId, group => group with
        {
            Status = ConfigurationMutationGroupStatus.RolledBack,
            RolledBackGroupId = rollbackGroupId,
            RolledBackTime = rolledBackTime
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        var groups = new List<ConfigurationMutationGroup>();
        foreach (var source in groupSources)
        {
            groups.AddRange(await source.ListAsync(from, to, definitionKey, cancellationToken));
        }

        return groups
            .OrderByDescending(group => group.CreatedTime)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken)
    {
        foreach (var source in groupSources)
        {
            var group = await source.GetAsync(groupId, cancellationToken);
            if (group is not null)
            {
                return group;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueHistory>> GetGroupHistoryAsync(string groupId, CancellationToken cancellationToken)
    {
        return historyService.QueryHistoryAsync(null, null, null, null, groupId, cancellationToken);
    }

    private async Task UpdateAsync(
        string groupId,
        Func<ConfigurationMutationGroup, ConfigurationMutationGroup> update,
        CancellationToken cancellationToken)
    {
        var source = ResolveWritableSource();
        var group = await source.GetAsync(groupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Configuration mutation group '{groupId}' was not found.");
        await source.UpsertAsync(update(group), cancellationToken);
    }

    private IConfigurationMutationGroupSource ResolveWritableSource()
    {
        return groupSources.FirstOrDefault()
            ?? throw new InvalidOperationException("No configuration mutation group store is registered.");
    }

    private static IReadOnlyList<string> NormalizeDefinitionKeys(IEnumerable<string> definitionKeys)
    {
        return definitionKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
