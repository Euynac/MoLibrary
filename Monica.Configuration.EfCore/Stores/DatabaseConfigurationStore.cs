using System.Data.Common;
using System.Reflection;
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
    private const int MAX_PUBLISH_RETRY_COUNT = 5;

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
        var candidates = definitions.Select(PublishedDefinitionCandidate.FromDefinition).ToArray();
        var changedCandidates = await GetChangedPublishCandidatesAsync(candidates, cancellationToken);
        foreach (var candidate in changedCandidates)
        {
            await PublishCandidateWithRetryAsync(candidate, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<PublishedDefinitionCandidate>> GetChangedPublishCandidatesAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        return await ExecuteAsync(async (dbContext, token) =>
        {
            var keys = candidates.Select(candidate => candidate.DefinitionKey).ToArray();
            var existing = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Where(definition => keys.Contains(definition.DefinitionKey))
                .ToDictionaryAsync(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase, token);

            return candidates
                .Where(candidate => !existing.TryGetValue(candidate.DefinitionKey, out var current)
                                    || !candidate.Matches(current))
                .ToArray();
        }, cancellationToken);
    }

    private async Task PublishCandidateWithRetryAsync(
        PublishedDefinitionCandidate candidate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MAX_PUBLISH_RETRY_COUNT; attempt++)
        {
            try
            {
                await ExecuteAsync(async (dbContext, token) =>
                {
                    var current = await dbContext.ConfigurationDefinitions
                        .FirstOrDefaultAsync(definition => definition.DefinitionKey == candidate.DefinitionKey, token);

                    if (current is null)
                    {
                        var created = candidate.CreateEntity();
                        dbContext.ConfigurationDefinitions.Add(created);
                        dbContext.ConfigurationDefinitionPublishHistories.Add(
                            candidate.CreateHistory(null, ConfigurationDefinitionPublishChangeKind.Created));
                        await dbContext.SaveChangesAsync(token);
                        return;
                    }

                    if (candidate.Matches(current))
                    {
                        return;
                    }

                    var changeKind = candidate.HasSameSchema(current)
                        ? ConfigurationDefinitionPublishChangeKind.MetadataChanged
                        : ConfigurationDefinitionPublishChangeKind.SchemaChanged;
                    var history = candidate.CreateHistory(current, changeKind);
                    candidate.ApplyTo(current, history.NewSchemaVersion);
                    dbContext.ConfigurationDefinitionPublishHistories.Add(history);
                    await dbContext.SaveChangesAsync(token);
                }, cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MAX_PUBLISH_RETRY_COUNT)
            {
                // Another instance published this definition first. Reload and compare against the new current row.
            }
            catch (DbUpdateException) when (attempt < MAX_PUBLISH_RETRY_COUNT)
            {
                // Most commonly the first-publish insert race. Retrying turns it into a normal compare/update pass.
            }
        }

        throw new InvalidOperationException(
            $"Failed to publish configuration definition '{candidate.DefinitionKey}' after {MAX_PUBLISH_RETRY_COUNT} attempts.");
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationDefinitionPublishHistory>> ListDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var normalizedLimit = Math.Clamp(limit, 1, 200);
            var histories = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .Where(history => history.DefinitionKey == definitionKey)
                .OrderByDescending(history => history.PublishedTime)
                .ThenByDescending(history => history.HistoryId)
                .Take(normalizedLimit)
                .ToArrayAsync(token);

            return histories.Select(ToPublishHistory).ToArray();
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
                .Select(definition => new { definition.SchemaJson, definition.FromProject, definition.Category, definition.PublishRevision })
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingTableException(ex))
        {
            return ConfigurationSchemaState.Missing;
        }
        catch (Exception ex) when (IsMissingColumnException(ex))
        {
            return ConfigurationSchemaState.Mismatch;
        }

        try
        {
            _ = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .Select(history => new { history.HistoryId, history.DefinitionKey, history.PublishedTime })
                .FirstOrDefaultAsync(cancellationToken);
            return ConfigurationSchemaState.Ready;
        }
        catch (Exception ex) when (IsMissingTableException(ex) || IsMissingColumnException(ex))
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
            typeof(ConfigurationDefinitionPublishHistoryEntity),
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

    private sealed record PublishedDefinitionCandidate
    {
        public required string DefinitionKey { get; init; }

        public required string SectionPath { get; init; }

        public required string DisplayName { get; init; }

        public required string ClrTypeName { get; init; }

        public required string FromProject { get; init; }

        public string? Category { get; init; }

        public int SourceSchemaVersion { get; init; }

        public required string SchemaHash { get; init; }

        public required string ReloadBehavior { get; init; }

        public required string SchemaJson { get; init; }

        public static PublishedDefinitionCandidate FromDefinition(ConfigurationDefinition definition)
        {
            return new PublishedDefinitionCandidate
            {
                DefinitionKey = definition.DefinitionKey,
                SectionPath = definition.SectionPath,
                DisplayName = definition.DisplayName,
                ClrTypeName = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(definition.ClrTypeName),
                FromProject = definition.FromProject,
                Category = NullIfWhiteSpace(definition.Category),
                SourceSchemaVersion = Math.Max(definition.SchemaVersion, 1),
                SchemaHash = definition.SchemaHash,
                ReloadBehavior = definition.ReloadBehavior.ToString(),
                SchemaJson = ConfigurationDefinitionSchemaCodec.SerializeSchema(definition)
            };
        }

        public bool Matches(ConfigurationDefinitionEntity current)
        {
            return HasSameSchema(current)
                   && string.Equals(SectionPath, current.SectionPath, StringComparison.Ordinal)
                   && string.Equals(DisplayName, current.DisplayName, StringComparison.Ordinal)
                   && string.Equals(ClrTypeName, current.ClrTypeName, StringComparison.Ordinal)
                   && string.Equals(FromProject, current.FromProject, StringComparison.Ordinal)
                   && string.Equals(Category, NullIfWhiteSpace(current.Category), StringComparison.Ordinal)
                   && string.Equals(ReloadBehavior, current.ReloadBehavior, StringComparison.Ordinal);
        }

        public bool HasSameSchema(ConfigurationDefinitionEntity current)
        {
            return string.Equals(SchemaHash, current.SchemaHash, StringComparison.Ordinal);
        }

        public ConfigurationDefinitionEntity CreateEntity()
        {
            var entity = new ConfigurationDefinitionEntity
            {
                DefinitionKey = DefinitionKey,
                SchemaVersion = SourceSchemaVersion,
                PublishRevision = 1
            };
            ApplySnapshot(entity, SourceSchemaVersion);
            return entity;
        }

        public void ApplyTo(ConfigurationDefinitionEntity entity, int schemaVersion)
        {
            ApplySnapshot(entity, schemaVersion);
            entity.PublishRevision = Math.Max(entity.PublishRevision, 0) + 1;
        }

        public ConfigurationDefinitionPublishHistoryEntity CreateHistory(
            ConfigurationDefinitionEntity? current,
            ConfigurationDefinitionPublishChangeKind changeKind)
        {
            var publisher = PublishActor.Capture();
            var newSchemaVersion = ResolveNewSchemaVersion(current, changeKind);
            return new ConfigurationDefinitionPublishHistoryEntity
            {
                HistoryId = Guid.NewGuid().ToString("N"),
                DefinitionKey = DefinitionKey,
                SectionPath = SectionPath,
                DisplayName = DisplayName,
                FromProject = FromProject,
                Category = Category,
                ChangeKind = changeKind.ToString(),
                PreviousSchemaVersion = current?.SchemaVersion,
                NewSchemaVersion = newSchemaVersion,
                PreviousSchemaHash = current?.SchemaHash,
                NewSchemaHash = SchemaHash,
                PreviousSchemaJson = current?.SchemaJson,
                NewSchemaJson = SchemaJson,
                ChangeSummaryJson = CreateChangeSummaryJson(current, changeKind),
                PublisherId = publisher.PublisherId,
                PublisherName = publisher.PublisherName,
                PublisherVersion = publisher.PublisherVersion,
                PublishedTime = DateTimeOffset.UtcNow
            };
        }

        private void ApplySnapshot(ConfigurationDefinitionEntity entity, int schemaVersion)
        {
            entity.SectionPath = SectionPath;
            entity.DisplayName = DisplayName;
            entity.ClrTypeName = ClrTypeName;
            entity.FromProject = FromProject;
            entity.Category = NullIfWhiteSpace(Category);
            entity.SchemaVersion = schemaVersion;
            entity.SchemaHash = SchemaHash;
            entity.ReloadBehavior = ReloadBehavior;
            entity.SchemaJson = SchemaJson;
        }

        private int ResolveNewSchemaVersion(
            ConfigurationDefinitionEntity? current,
            ConfigurationDefinitionPublishChangeKind changeKind)
        {
            if (current is null)
            {
                return SourceSchemaVersion;
            }

            var currentVersion = Math.Max(Math.Max(current.SchemaVersion, SourceSchemaVersion), 1);
            return changeKind == ConfigurationDefinitionPublishChangeKind.SchemaChanged
                ? currentVersion + 1
                : currentVersion;
        }

        private string CreateChangeSummaryJson(
            ConfigurationDefinitionEntity? current,
            ConfigurationDefinitionPublishChangeKind changeKind)
        {
            var changes = new List<SchemaPublishChangeDto>();
            if (current is null)
            {
                changes.Add(new SchemaPublishChangeDto("Definition", null, DefinitionKey));
            }
            else
            {
                AddChange(changes, nameof(SectionPath), current.SectionPath, SectionPath);
                AddChange(changes, nameof(DisplayName), current.DisplayName, DisplayName);
                AddChange(changes, nameof(ClrTypeName), current.ClrTypeName, ClrTypeName);
                AddChange(changes, nameof(FromProject), current.FromProject, FromProject);
                AddChange(changes, nameof(Category), NullIfWhiteSpace(current.Category), Category);
                AddChange(changes, nameof(ReloadBehavior), current.ReloadBehavior, ReloadBehavior);
                AddChange(changes, nameof(SchemaHash), current.SchemaHash, SchemaHash);
            }

            var summary = new SchemaPublishChangeSummaryDto(changeKind.ToString(), changes);
            return JsonSerializer.Serialize(summary, ConfigurationPersistedJsonOptions.CompactSchema);
        }

        private static void AddChange(
            List<SchemaPublishChangeDto> changes,
            string field,
            string? previousValue,
            string? newValue)
        {
            if (!string.Equals(previousValue, newValue, StringComparison.Ordinal))
            {
                changes.Add(new SchemaPublishChangeDto(field, previousValue, newValue));
            }
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    private sealed record PublishActor(string PublisherId, string PublisherName, string? PublisherVersion)
    {
        public static PublishActor Capture()
        {
            var publisherName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_NAME"),
                Environment.GetEnvironmentVariable("HOSTNAME"),
                Environment.MachineName);
            var publisherId = FirstNonEmpty(
                Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_ID"),
                $"{publisherName}:{Environment.ProcessId}");
            return new PublishActor(publisherId, publisherName, GetEntryAssemblyVersion());
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "unknown";
        }

        private static string? GetEntryAssemblyVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly?.GetName().Version?.ToString();
        }
    }

    private sealed record SchemaPublishChangeSummaryDto(
        string ChangeKind,
        IReadOnlyList<SchemaPublishChangeDto> Changes);

    private sealed record SchemaPublishChangeDto(
        string Field,
        string? PreviousValue,
        string? NewValue);

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

    private static ConfigurationDefinitionPublishHistory ToPublishHistory(
        ConfigurationDefinitionPublishHistoryEntity entity)
    {
        return new ConfigurationDefinitionPublishHistory
        {
            HistoryId = entity.HistoryId,
            DefinitionKey = entity.DefinitionKey,
            SectionPath = entity.SectionPath,
            DisplayName = entity.DisplayName,
            FromProject = entity.FromProject,
            Category = entity.Category,
            ChangeKind = Enum.Parse<ConfigurationDefinitionPublishChangeKind>(entity.ChangeKind),
            PreviousSchemaVersion = entity.PreviousSchemaVersion,
            NewSchemaVersion = entity.NewSchemaVersion,
            PreviousSchemaHash = entity.PreviousSchemaHash,
            NewSchemaHash = entity.NewSchemaHash,
            PreviousSchemaJson = entity.PreviousSchemaJson,
            NewSchemaJson = entity.NewSchemaJson,
            ChangeSummaryJson = entity.ChangeSummaryJson,
            PublisherId = entity.PublisherId,
            PublisherName = entity.PublisherName,
            PublisherVersion = entity.PublisherVersion,
            PublishedTime = entity.PublishedTime
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
