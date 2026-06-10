using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Modules;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Stores;

/// <summary>
/// EF Core-backed store bundle for distributed Monica.Configuration deployments.
/// </summary>
public sealed class DatabaseConfigurationStore(
    IDbContextOperation<ConfigurationDbContext> dbContextOperation,
    IOptions<ModuleConfigurationEfCoreOption> options)
    : IConfigurationEffectiveValueStore, IConfigurationHistoryStore, IConfigurationMetadataStore
{
    private readonly SemaphoreSlim _schemaInitializationLock = new(1, 1);
    private bool _schemaInitialized;

    private enum ConfigurationSchemaState
    {
        Missing,
        Mismatch,
        Ready
    }

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
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationEffectiveValues
                .FirstOrDefaultAsync(value => value.DefinitionKey == definition.DefinitionKey, token);
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
            await dbContext.SaveChangesAsync(token);
            return ToDocument(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument?> GetAsync(string definitionKey, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationEffectiveValues
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.DefinitionKey == definitionKey, token);
            return entity is null ? null : ToDocument(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> SaveAsync(
        ConfigurationEffectiveValueSaveRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationEffectiveValues
                .FirstOrDefaultAsync(value => value.DefinitionKey == request.Definition.DefinitionKey, token);
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

            await dbContext.SaveChangesAsync(token);
            return ToDocument(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendHistoryAsync(ConfigurationValueHistory history, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async (dbContext, token) =>
        {
            dbContext.ConfigurationValueHistories.Add(ToEntity(history));
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
        return await ExecuteAsync(async (dbContext, token) =>
        {
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
                .ToArrayAsync(token);
            return entities.Select(ToHistory).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationValueHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(history => history.HistoryId == historyId, token);
            return entity is null ? null : ToHistory(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationMutationGroups
                .FirstOrDefaultAsync(candidate => candidate.GroupId == group.GroupId, token);
            if (entity is null)
            {
                entity = new ConfigurationMutationGroupEntity { GroupId = group.GroupId };
                dbContext.ConfigurationMutationGroups.Add(entity);
            }

            entity.Label = group.Label;
            entity.Reason = group.Reason;
            entity.DefinitionKeysJson = JsonSerializer.Serialize(group.DefinitionKeys, ConfigurationPersistedJsonOptions.CompactValue);
            entity.MutationCount = group.MutationCount;
            entity.CreatedTime = group.CreatedTime;
            entity.ModifierId = group.ModifierId;
            entity.ModifierName = group.ModifierName;
            entity.RolledBackTime = group.RolledBackTime;
            entity.RolledBackGroupId = group.RolledBackGroupId;
            entity.Status = group.Status.ToString();
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
        return await ExecuteAsync(async (dbContext, token) =>
        {
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
                    .ToArrayAsync(token))
                .Select(ToGroup)
                .ToArray();

            return string.IsNullOrWhiteSpace(definitionKey)
                ? groups
                : groups.Where(group => group.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase)).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationMutationGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(group => group.GroupId == groupId, token);
            return entity is null ? null : ToGroup(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken)
    {
        await ExecuteAsync(async (dbContext, token) =>
        {
            foreach (var definition in definitions)
            {
                var entity = await dbContext.ConfigurationDefinitions
                    .FirstOrDefaultAsync(x => x.DefinitionKey == definition.DefinitionKey, token);

                if (entity is null)
                {
                    entity = new ConfigurationDefinitionEntity { DefinitionKey = definition.DefinitionKey };
                    dbContext.ConfigurationDefinitions.Add(entity);
                }

                entity.SectionPath = definition.SectionPath;
                entity.DisplayName = definition.DisplayName;
                entity.ClrTypeName = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(definition.ClrTypeName);
                entity.FromProject = definition.FromProject;
                entity.Category = definition.Category;
                entity.SchemaVersion = definition.SchemaVersion;
                entity.SchemaHash = definition.SchemaHash;
                entity.ReloadBehavior = definition.ReloadBehavior.ToString();
                entity.SchemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition);
                entity.LastSeenTime = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync(token);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationDefinition>> ListPublishedDefinitionsAsync(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entities = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .OrderBy(definition => definition.DisplayName)
                .ThenBy(definition => definition.DefinitionKey)
                .ToArrayAsync(token);
            return entities.Select(ToDefinition).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationDefinition?> GetPublishedDefinitionAsync(string definitionKey, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(definition => definition.DefinitionKey == definitionKey, token);
            return entity is null ? null : ToDefinition(entity);
        }, cancellationToken);
    }

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<ConfigurationDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        return await dbContextOperation.ExecuteAsync(operation, cancellationToken);
    }

    private async Task ExecuteAsync(
        Func<ConfigurationDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await dbContextOperation.ExecuteAsync(operation, cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.AutoCreateSchema || _schemaInitialized)
        {
            return;
        }

        await _schemaInitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            await dbContextOperation.ExecuteAsync(async (dbContext, token) =>
            {
                var creator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
                if (!await creator.ExistsAsync(token))
                {
                    await creator.CreateAsync(token);
                    await creator.CreateTablesAsync(token);
                    return;
                }

                var schemaState = await GetConfigurationSchemaStateAsync(dbContext, token);
                if (schemaState == ConfigurationSchemaState.Missing)
                {
                    await creator.CreateTablesAsync(token);
                    return;
                }

                if (schemaState == ConfigurationSchemaState.Mismatch)
                {
                    await DropConfigurationTablesAsync(dbContext, token);
                    await creator.CreateTablesAsync(token);
                }
            }, cancellationToken);

            _schemaInitialized = true;
        }
        finally
        {
            _schemaInitializationLock.Release();
        }
    }

    private static async Task<ConfigurationSchemaState> GetConfigurationSchemaStateAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Select(definition => new { definition.SchemaJson, definition.FromProject, definition.Category })
                .FirstOrDefaultAsync(cancellationToken);
            return ConfigurationSchemaState.Ready;
        }
        catch (Exception ex) when (IsMissingTableException(ex))
        {
            return ConfigurationSchemaState.Missing;
        }
        catch (Exception ex) when (IsMissingColumnException(ex))
        {
            return ConfigurationSchemaState.Mismatch;
        }
    }

    private static async Task DropConfigurationTablesAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var entityTypes = new[]
        {
            typeof(ConfigurationValueHistoryEntity),
            typeof(ConfigurationMutationGroupEntity),
            typeof(ConfigurationEffectiveValueEntity),
            typeof(ConfigurationDefinitionEntity)
        };

        foreach (var entityType in entityTypes)
        {
            var entity = dbContext.Model.FindEntityType(entityType)
                ?? throw new InvalidOperationException($"EF entity '{entityType.Name}' is not part of {nameof(ConfigurationDbContext)}.");
            var tableName = entity.GetTableName()
                ?? throw new InvalidOperationException($"EF entity '{entityType.Name}' does not have a table name.");
            var tableSql = FormatTableName(dbContext.Database.ProviderName, entity.GetSchema(), tableName);
            var commandText = "DROP TABLE IF EXISTS " + tableSql;
            await dbContext.Database.ExecuteSqlRawAsync(commandText, cancellationToken);
        }
    }

    private static bool IsMissingTableException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException dbException && IsMissingTableException(dbException))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingColumnException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException dbException && IsMissingColumnException(dbException))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingTableException(DbException exception)
    {
        return GetStringProperty(exception, "SqlState") is "42P01"
            || GetIntProperty(exception, "Number") is 208 or 1146
            || GetIntProperty(exception, "SqliteErrorCode") is 1
                && exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingColumnException(DbException exception)
    {
        return GetStringProperty(exception, "SqlState") is "42703"
            || GetIntProperty(exception, "Number") is 207 or 1054
            || GetIntProperty(exception, "SqliteErrorCode") is 1
                && exception.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatTableName(string? providerName, string? schema, string tableName)
    {
        if (providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true)
        {
            return string.IsNullOrWhiteSpace(schema)
                ? QuoteSqlServer(tableName)
                : $"{QuoteSqlServer(schema)}.{QuoteSqlServer(tableName)}";
        }

        if (providerName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true)
        {
            return string.IsNullOrWhiteSpace(schema)
                ? QuoteMySql(tableName)
                : $"{QuoteMySql(schema)}.{QuoteMySql(tableName)}";
        }

        return string.IsNullOrWhiteSpace(schema)
            ? QuoteAnsi(tableName)
            : $"{QuoteAnsi(schema)}.{QuoteAnsi(tableName)}";
    }

    private static string QuoteSqlServer(string identifier)
    {
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string QuoteMySql(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private static string QuoteAnsi(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as string;
    }

    private static int? GetIntProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as int?;
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

    private static ConfigurationDefinition ToDefinition(ConfigurationDefinitionEntity entity)
    {
        return ConfigurationDefinitionSchemaCodec.DeserializeDefinition(
            entity.DefinitionKey,
            entity.SectionPath,
            entity.DisplayName,
            entity.ClrTypeName,
            entity.FromProject,
            entity.Category,
            entity.SchemaVersion,
            entity.SchemaHash,
            Enum.Parse<ConfigurationReloadBehavior>(entity.ReloadBehavior),
            entity.SchemaJson,
            ConfigurationDefinitionOrigin.PublishedMetadata);
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
            TargetKind = history.TargetKind.ToString(),
            SourceProviderType = history.SourceProviderType,
            SourceDisplayName = history.SourceDisplayName,
            SourcePhysicalPath = history.SourcePhysicalPath,
            SourceConfigurationPath = history.SourceConfigurationPath,
            MutationKind = history.MutationKind.ToString(),
            Granularity = history.Granularity.ToString(),
            State = history.State.ToString(),
            OldValueJson = history.OldValue is null ? null : JsonSerializer.Serialize(history.OldValue, ConfigurationPersistedJsonOptions.CompactValue),
            NewValueJson = JsonSerializer.Serialize(history.NewValue, ConfigurationPersistedJsonOptions.CompactValue),
            Version = history.Version,
            SourceRevisionBefore = history.SourceRevisionBefore,
            SourceRevisionAfter = history.SourceRevisionAfter,
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
            TargetKind = string.IsNullOrWhiteSpace(entity.TargetKind)
                ? ConfigurationMutationTargetKind.MonicaEffectiveStore
                : Enum.Parse<ConfigurationMutationTargetKind>(entity.TargetKind),
            SourceProviderType = entity.SourceProviderType,
            SourceDisplayName = entity.SourceDisplayName,
            SourcePhysicalPath = entity.SourcePhysicalPath,
            SourceConfigurationPath = entity.SourceConfigurationPath,
            MutationKind = Enum.Parse<ConfigurationMutationKind>(entity.MutationKind),
            Granularity = Enum.Parse<ConfigurationMutationGranularity>(entity.Granularity),
            State = Enum.Parse<ConfigurationValueState>(entity.State),
            OldValue = JsonToStoredValue(entity.OldValueJson),
            NewValue = JsonToStoredValue(entity.NewValueJson) ?? ConfigurationStoredValue.Null,
            Version = entity.Version,
            SourceRevisionBefore = entity.SourceRevisionBefore,
            SourceRevisionAfter = entity.SourceRevisionAfter,
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
            DefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.DefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
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
            : JsonSerializer.Deserialize<ConfigurationStoredValue>(json, ConfigurationPersistedJsonOptions.CompactValue);
    }

    private static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return JsonSerializer.Serialize(document.RootElement, ConfigurationPersistedJsonOptions.CompactValue);
    }
}
