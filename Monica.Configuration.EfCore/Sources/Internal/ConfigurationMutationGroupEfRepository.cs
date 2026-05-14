using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Sources.Internal;

/// <summary>
/// EF Core repository for persisted configuration mutation groups.
/// </summary>
internal sealed class ConfigurationMutationGroupEfRepository(IDbContextProvider<ConfigurationDbContext> dbContextProvider)
{
    public async Task UpsertAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationMutationGroups
            .FirstOrDefaultAsync(x => x.GroupId == group.GroupId, cancellationToken);
        if (entity is null)
        {
            entity = new ConfigurationMutationGroupEntity
            {
                GroupId = group.GroupId
            };
            dbContext.ConfigurationMutationGroups.Add(entity);
        }

        UpdateEntity(entity, group);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConfigurationMutationGroup?> GetAsync(string groupId, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationMutationGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GroupId == groupId, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<ConfigurationMutationGroup>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var query = dbContext.ConfigurationMutationGroups.AsNoTracking();

        if (from is not null)
        {
            query = query.Where(group => group.CreatedTime >= from);
        }

        if (to is not null)
        {
            query = query.Where(group => group.CreatedTime <= to);
        }

        var entities = await query
            .OrderByDescending(group => group.CreatedTime)
            .ToListAsync(cancellationToken);
        var groups = entities.Select(ToModel);

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            groups = groups.Where(group => group.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase));
        }

        return groups.ToArray();
    }

    private static void UpdateEntity(ConfigurationMutationGroupEntity entity, ConfigurationMutationGroup group)
    {
        entity.Label = group.Label;
        entity.Reason = group.Reason;
        entity.DefinitionKeysJson = JsonSerializer.Serialize(group.DefinitionKeys);
        entity.MutationCount = group.MutationCount;
        entity.CreatedTime = group.CreatedTime;
        entity.ModifierId = group.ModifierId;
        entity.ModifierName = group.ModifierName;
        entity.RolledBackTime = group.RolledBackTime;
        entity.RolledBackGroupId = group.RolledBackGroupId;
        entity.Status = group.Status.ToString();
    }

    private static ConfigurationMutationGroup ToModel(ConfigurationMutationGroupEntity entity)
    {
        return new ConfigurationMutationGroup
        {
            GroupId = entity.GroupId,
            Label = entity.Label,
            Reason = entity.Reason,
            DefinitionKeys = ReadDefinitionKeys(entity.DefinitionKeysJson),
            MutationCount = entity.MutationCount,
            CreatedTime = entity.CreatedTime,
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            RolledBackTime = entity.RolledBackTime,
            RolledBackGroupId = entity.RolledBackGroupId,
            Status = Enum.Parse<ConfigurationMutationGroupStatus>(entity.Status)
        };
    }

    private static IReadOnlyList<string> ReadDefinitionKeys(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
    }
}
