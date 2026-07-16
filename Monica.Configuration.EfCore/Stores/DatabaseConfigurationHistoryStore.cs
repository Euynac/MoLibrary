using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.EfCore.Stores.Support;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores;

internal sealed class DatabaseConfigurationHistoryStore(ConfigurationDatabase database)
    : IConfigurationHistoryStore
{
    private const int HISTORY_ID_QUERY_BATCH_SIZE = 500;

    /// <inheritdoc />
    public ConfigurationStoreDescriptor Descriptor => ConfigurationDatabase.Descriptor;

    /// <inheritdoc />
    public async Task AppendHistoryAsync(
        ConfigurationValueHistory history,
        CancellationToken cancellationToken)
    {
        await database.ExecuteAsync(async (dbContext, token) =>
        {
            dbContext.ConfigurationValueHistories.Add(ConfigurationHistoryMapper.ToEntity(history));
            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var entities = await ApplyHistoryFilters(
                    dbContext.ConfigurationValueHistories.AsNoTracking(),
                    from,
                    to,
                    definitionKey,
                    logicalPath,
                    mutationGroupId,
                    targetKind: null)
                .OrderByDescending(history => history.ModifiedTime)
                .ThenByDescending(history => history.Version)
                .ThenByDescending(history => history.HistoryId)
                .ToArrayAsync(token);
            return entities.Select(ConfigurationHistoryMapper.ToHistory).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationHistoryPageResult> QueryHistoryPageAsync(
        ConfigurationHistoryPageRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var historyQuery = ApplyHistoryFilters(
                dbContext.ConfigurationValueHistories.AsNoTracking(),
                request.From,
                request.To,
                request.DefinitionKey,
                request.LogicalPath,
                request.MutationGroupId,
                request.TargetKind);
            var unitQuery = historyQuery
                .GroupBy(history => new
                {
                    UnitKind = history.MutationGroupId == null
                        ? (int)ConfigurationHistoryUnitKind.StandaloneHistory
                        : (int)ConfigurationHistoryUnitKind.MutationGroup,
                    UnitId = history.MutationGroupId ?? history.HistoryId
                })
                .Select(group => new
                {
                    group.Key.UnitKind,
                    group.Key.UnitId,
                    ModifiedTime = group.Max(history => history.ModifiedTime),
                    Version = group.Max(history => history.Version)
                });
            if (request.Cursor is { } cursor)
            {
                var cursorTime = cursor.ModifiedTime.UtcDateTime;
                var cursorUnitKind = (int)cursor.UnitKind;
                unitQuery = unitQuery.Where(unit =>
                    unit.ModifiedTime < cursorTime
                    || unit.ModifiedTime == cursorTime && unit.Version < cursor.Version
                    || unit.ModifiedTime == cursorTime && unit.Version == cursor.Version
                    && unit.UnitKind < cursorUnitKind
                    || unit.ModifiedTime == cursorTime && unit.Version == cursor.Version
                    && unit.UnitKind == cursorUnitKind
                    && string.Compare(unit.UnitId, cursor.UnitId) < 0);
            }

            var candidates = await unitQuery
                .OrderByDescending(unit => unit.ModifiedTime)
                .ThenByDescending(unit => unit.Version)
                .ThenByDescending(unit => unit.UnitKind)
                .ThenByDescending(unit => unit.UnitId)
                .Take(request.PageSize + 1)
                .ToArrayAsync(token);
            var selectedUnits = candidates.Take(request.PageSize).ToArray();
            if (selectedUnits.Length == 0)
            {
                return new ConfigurationHistoryPageResult
                {
                    Items = [],
                    NextCursor = null,
                    HasMore = false
                };
            }

            var groupIds = selectedUnits
                .Where(static unit => unit.UnitKind == (int)ConfigurationHistoryUnitKind.MutationGroup)
                .Select(static unit => unit.UnitId)
                .ToArray();
            var standaloneHistoryIds = selectedUnits
                .Where(static unit => unit.UnitKind == (int)ConfigurationHistoryUnitKind.StandaloneHistory)
                .Select(static unit => unit.UnitId)
                .ToArray();
            var entities = await historyQuery
                .Where(history =>
                    history.MutationGroupId != null && groupIds.Contains(history.MutationGroupId)
                    || history.MutationGroupId == null && standaloneHistoryIds.Contains(history.HistoryId))
                .OrderByDescending(history => history.ModifiedTime)
                .ThenByDescending(history => history.Version)
                .ThenByDescending(history => history.HistoryId)
                .ToArrayAsync(token);
            var hasMore = candidates.Length > request.PageSize;
            var lastUnit = selectedUnits[^1];
            return new ConfigurationHistoryPageResult
            {
                Items = entities.Select(ConfigurationHistoryMapper.ToHistory).ToArray(),
                NextCursor = hasMore
                    ? new ConfigurationHistoryCursor
                    {
                        ModifiedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(lastUnit.ModifiedTime),
                        Version = lastUnit.Version,
                        UnitKind = (ConfigurationHistoryUnitKind)lastUnit.UnitKind,
                        UnitId = lastUnit.UnitId
                    }
                    : null,
                HasMore = hasMore
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(
        string historyId,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationValueHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(history => history.HistoryId == historyId, token);
            return entity is null ? null : ConfigurationHistoryMapper.ToHistory(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoriesByIdsAsync(
        IReadOnlyCollection<string> historyIds,
        CancellationToken cancellationToken)
    {
        if (historyIds.Count == 0)
        {
            return [];
        }

        var distinctIds = historyIds.Distinct(StringComparer.Ordinal).ToArray();
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var histories = new List<ConfigurationValueHistory>(distinctIds.Length);
            foreach (var idBatch in distinctIds.Chunk(HISTORY_ID_QUERY_BATCH_SIZE))
            {
                var entities = await dbContext.ConfigurationValueHistories
                    .AsNoTracking()
                    .Where(history => idBatch.Contains(history.HistoryId))
                    .ToArrayAsync(token);
                histories.AddRange(entities.Select(ConfigurationHistoryMapper.ToHistory));
            }

            return histories;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertGroupAsync(
        ConfigurationMutationGroup group,
        CancellationToken cancellationToken)
    {
        await database.ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationMutationGroups
                .FirstOrDefaultAsync(candidate => candidate.GroupId == group.GroupId, token);
            if (entity is null)
            {
                entity = new ConfigurationMutationGroupEntity { GroupId = group.GroupId };
                dbContext.ConfigurationMutationGroups.Add(entity);
            }

            ConfigurationHistoryMapper.ApplyTo(group, entity);
            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationGroup>> ListGroupsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var groups = await ApplyGroupFilters(
                    dbContext.ConfigurationMutationGroups.AsNoTracking(),
                    dbContext,
                    from,
                    to,
                    definitionKey)
                .OrderByDescending(group => group.CreatedTime)
                .ThenByDescending(group => group.GroupId)
                .ToArrayAsync(token);
            return groups.Select(ConfigurationHistoryMapper.ToGroup).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroupPageResult> QueryGroupsPageAsync(
        ConfigurationMutationGroupPageRequest request,
        CancellationToken cancellationToken)
    {
        request.Validate();
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var query = ApplyGroupFilters(
                dbContext.ConfigurationMutationGroups.AsNoTracking(),
                dbContext,
                request.From,
                request.To,
                request.DefinitionKey);
            if (request.Cursor is { } cursor)
            {
                var cursorTime = cursor.CreatedTime.UtcDateTime;
                query = query.Where(group =>
                    group.CreatedTime < cursorTime
                    || group.CreatedTime == cursorTime
                    && string.Compare(group.GroupId, cursor.GroupId) < 0);
            }

            var candidates = await query
                .OrderByDescending(group => group.CreatedTime)
                .ThenByDescending(group => group.GroupId)
                .Take(request.PageSize + 1)
                .ToArrayAsync(token);
            var selectedGroups = candidates.Take(request.PageSize).ToArray();
            var hasMore = candidates.Length > request.PageSize;
            var lastGroup = selectedGroups.LastOrDefault();
            return new ConfigurationMutationGroupPageResult
            {
                Items = selectedGroups.Select(ConfigurationHistoryMapper.ToGroup).ToArray(),
                NextCursor = hasMore && lastGroup is not null
                    ? new ConfigurationMutationGroupCursor
                    {
                        CreatedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(lastGroup.CreatedTime),
                        GroupId = lastGroup.GroupId
                    }
                    : null,
                HasMore = hasMore
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup?> GetGroupAsync(
        string groupId,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationMutationGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(group => group.GroupId == groupId, token);
            return entity is null ? null : ConfigurationHistoryMapper.ToGroup(entity);
        }, cancellationToken);
    }

    private static IQueryable<ConfigurationValueHistoryEntity> ApplyHistoryFilters(
        IQueryable<ConfigurationValueHistoryEntity> query,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        LogicalPath? logicalPath,
        string? mutationGroupId,
        ConfigurationMutationTargetKind? targetKind)
    {
        if (from is not null)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(history => history.ModifiedTime >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(history => history.ModifiedTime <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
            query = query.Where(history => history.DefinitionIdentity == definitionIdentity);
        }

        if (logicalPath is not null)
        {
            var canonicalPath = logicalPath.ToCanonicalString();
            query = query.Where(history => history.LogicalPath == canonicalPath);
        }

        if (!string.IsNullOrWhiteSpace(mutationGroupId))
        {
            query = query.Where(history => history.MutationGroupId == mutationGroupId);
        }

        if (targetKind is not null)
        {
            var targetKindName = targetKind.Value.ToString();
            query = targetKind == ConfigurationMutationTargetKind.MonicaEffectiveStore
                ? query.Where(history => history.TargetKind == targetKindName
                                         || string.IsNullOrEmpty(history.TargetKind))
                : query.Where(history => history.TargetKind == targetKindName);
        }

        return query;
    }

    private static IQueryable<ConfigurationMutationGroupEntity> ApplyGroupFilters(
        IQueryable<ConfigurationMutationGroupEntity> query,
        ConfigurationDbContext dbContext,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey)
    {
        if (from is not null)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(group => group.CreatedTime >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(group => group.CreatedTime <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
            query = query.Where(group => dbContext.ConfigurationValueHistories.Any(history =>
                history.MutationGroupId == group.GroupId
                && history.DefinitionIdentity == definitionIdentity));
        }

        return query;
    }
}
