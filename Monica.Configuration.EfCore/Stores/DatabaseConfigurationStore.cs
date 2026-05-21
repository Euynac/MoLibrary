using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Stores;

/// <summary>
/// EF Core-backed store bundle for distributed Monica.Configuration deployments.
/// </summary>
public sealed class DatabaseConfigurationStore(IDbContextProvider<ConfigurationDbContext> dbContextProvider)
    : IConfigurationEffectiveValueStore, IConfigurationHistoryStore, IConfigurationMetadataStore
{
    /// <inheritdoc />
    public ConfigurationStoreDescriptor Descriptor { get; } = new()
    {
        StoreKey = "db:default",
        DisplayName = "Database",
        Kind = ConfigurationStoreKind.Database,
        SupportsEffectiveValues = true,
        SupportsHistory = true,
        SupportsMetadata = true
    };

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
        ConfigurationDefinition definition,
        string seedJson,
        CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationEffectiveValues
            .FirstOrDefaultAsync(value => value.DefinitionKey == definition.DefinitionKey, cancellationToken);
        if (entity is not null)
        {
            return ToDocument(entity);
        }

        entity = new ConfigurationEffectiveValueEntity
        {
            DefinitionKey = definition.DefinitionKey,
            Json = NormalizeJson(seedJson),
            Version = 1,
            SchemaVersion = definition.SchemaVersion,
            LastModifiedTime = DateTimeOffset.UtcNow
        };
        dbContext.ConfigurationEffectiveValues.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDocument(entity);
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument?> GetAsync(string definitionKey, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationEffectiveValues
            .AsNoTracking()
            .FirstOrDefaultAsync(value => value.DefinitionKey == definitionKey, cancellationToken);
        return entity is null ? null : ToDocument(entity);
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> SaveAsync(
        ConfigurationEffectiveValueSaveRequest request,
        CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationEffectiveValues
            .FirstOrDefaultAsync(value => value.DefinitionKey == request.Definition.DefinitionKey, cancellationToken);
        if (request.ExpectedVersion is not null && entity?.Version != request.ExpectedVersion)
        {
            throw new ConfigurationConcurrencyConflictException(
                $"Expected version {request.ExpectedVersion} for '{request.Definition.DefinitionKey}', but current version is {entity?.Version.ToString() ?? "<none>"}.");
        }

        if (entity is null)
        {
            entity = new ConfigurationEffectiveValueEntity { DefinitionKey = request.Definition.DefinitionKey };
            dbContext.ConfigurationEffectiveValues.Add(entity);
        }

        entity.Json = NormalizeJson(request.Json);
        entity.Version++;
        entity.SchemaVersion = request.Definition.SchemaVersion;
        entity.LastModifiedTime = DateTimeOffset.UtcNow;
        entity.LastModifierId = request.Context.ModifierId;
        entity.LastModifierName = request.Context.ModifierName;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDocument(entity);
    }

    /// <inheritdoc />
    public async Task AppendHistoryAsync(ConfigurationValueHistory history, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        dbContext.ConfigurationValueHistories.Add(ToEntity(history));
        await dbContext.SaveChangesAsync(cancellationToken);
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
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var query = dbContext.ConfigurationValueHistories.AsNoTracking();

        if (from is not null)
        {
            query = query.Where(history => history.ModifiedTime >= from);
        }

        if (to is not null)
        {
            query = query.Where(history => history.ModifiedTime <= to);
        }

        if (!string.IsNullOrWhiteSpace(definitionKey))
        {
            query = query.Where(history => history.DefinitionKey == definitionKey);
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

        var entities = await query
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ToArrayAsync(cancellationToken);
        return entities.Select(ToHistory).ToArray();
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationValueHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(history => history.HistoryId == historyId, cancellationToken);
        return entity is null ? null : ToHistory(entity);
    }

    /// <inheritdoc />
    public async Task UpsertGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationMutationGroups
            .FirstOrDefaultAsync(candidate => candidate.GroupId == group.GroupId, cancellationToken);
        if (entity is null)
        {
            entity = new ConfigurationMutationGroupEntity { GroupId = group.GroupId };
            dbContext.ConfigurationMutationGroups.Add(entity);
        }

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
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationMutationGroup>> ListGroupsAsync(
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

        var groups = (await query
                .OrderByDescending(group => group.CreatedTime)
                .ToArrayAsync(cancellationToken))
            .Select(ToGroup)
            .ToArray();

        return string.IsNullOrWhiteSpace(definitionKey)
            ? groups
            : groups.Where(group => group.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entity = await dbContext.ConfigurationMutationGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.GroupId == groupId, cancellationToken);
        return entity is null ? null : ToGroup(entity);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        foreach (var definition in definitions)
        {
            var entity = await dbContext.ConfigurationDefinitions
                .FirstOrDefaultAsync(x => x.DefinitionKey == definition.DefinitionKey, cancellationToken);

            if (entity is null)
            {
                entity = new ConfigurationDefinitionEntity { DefinitionKey = definition.DefinitionKey };
                dbContext.ConfigurationDefinitions.Add(entity);
            }

            entity.SectionPath = definition.SectionPath;
            entity.DisplayName = definition.DisplayName;
            entity.ClrTypeName = definition.ClrTypeName;
            entity.OwnerModule = definition.OwnerModule;
            entity.Category = definition.Category;
            entity.SchemaVersion = definition.SchemaVersion;
            entity.SchemaHash = definition.SchemaHash;
            entity.ReloadBehavior = definition.ReloadBehavior.ToString();
            entity.DefinitionJson = JsonSerializer.Serialize(definition);
            entity.LastSeenTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ConfigurationEffectiveValueDocument ToDocument(ConfigurationEffectiveValueEntity entity)
    {
        return new ConfigurationEffectiveValueDocument
        {
            DefinitionKey = entity.DefinitionKey,
            Json = entity.Json,
            Version = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            LastModifiedTime = entity.LastModifiedTime,
            LastModifierId = entity.LastModifierId,
            LastModifierName = entity.LastModifierName
        };
    }

    private ConfigurationValueHistoryEntity ToEntity(ConfigurationValueHistory history)
    {
        return new ConfigurationValueHistoryEntity
        {
            HistoryId = history.HistoryId,
            DefinitionKey = history.DefinitionKey,
            LogicalPath = history.LogicalPath.ToCanonicalString(),
            PathDepth = history.LogicalPath.Depth,
            ConfigurationPath = history.ConfigurationPath,
            MutationKind = history.MutationKind.ToString(),
            Granularity = history.Granularity.ToString(),
            State = history.State.ToString(),
            OldValueJson = history.OldValue is null ? null : JsonSerializer.Serialize(history.OldValue),
            NewValueJson = JsonSerializer.Serialize(history.NewValue),
            Version = history.Version,
            SchemaVersion = history.SchemaVersion,
            ModifiedTime = history.ModifiedTime,
            ModifierId = history.ModifierId,
            ModifierName = history.ModifierName,
            Reason = history.Reason,
            MutationGroupId = history.MutationGroupId
        };
    }

    private static ConfigurationValueHistory ToHistory(ConfigurationValueHistoryEntity entity)
    {
        return new ConfigurationValueHistory
        {
            HistoryId = entity.HistoryId,
            DefinitionKey = entity.DefinitionKey,
            LogicalPath = LogicalPath.Parse(entity.LogicalPath),
            ConfigurationPath = entity.ConfigurationPath,
            MutationKind = Enum.Parse<ConfigurationMutationKind>(entity.MutationKind),
            Granularity = Enum.Parse<ConfigurationMutationGranularity>(entity.Granularity),
            State = Enum.Parse<ConfigurationValueState>(entity.State),
            OldValue = JsonToStoredValue(entity.OldValueJson),
            NewValue = JsonToStoredValue(entity.NewValueJson) ?? ConfigurationStoredValue.Null,
            Version = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            ModifiedTime = entity.ModifiedTime,
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            Reason = entity.Reason,
            MutationGroupId = entity.MutationGroupId
        };
    }

    private static ConfigurationMutationGroup ToGroup(ConfigurationMutationGroupEntity entity)
    {
        return new ConfigurationMutationGroup
        {
            GroupId = entity.GroupId,
            Label = entity.Label,
            Reason = entity.Reason,
            DefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(entity.DefinitionKeysJson) ?? [],
            MutationCount = entity.MutationCount,
            CreatedTime = entity.CreatedTime,
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            RolledBackTime = entity.RolledBackTime,
            RolledBackGroupId = entity.RolledBackGroupId,
            Status = Enum.Parse<ConfigurationMutationGroupStatus>(entity.Status)
        };
    }

    private static ConfigurationStoredValue? JsonToStoredValue(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ConfigurationStoredValue>(json);
    }

    private static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.GetRawText();
    }
}
