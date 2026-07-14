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
    : IConfigurationEffectiveValueStore, IConfigurationHistoryStore, IConfigurationMetadataStore,
        IConfigurationUnifiedVersionStore, IConfigurationMutationBatchStore
{
    private const int MAX_PUBLISH_RETRY_COUNT = 5;
    private const int MAX_EFFECTIVE_VALUE_ENSURE_RETRY_COUNT = 5;
    private const int MAX_UNIFIED_VERSION_APPEND_RETRY_COUNT = 5;
    private const string PUBLISH_DEFINITIONS_LOCK_MARKER_KEY = "Configuration.EfCore.PublishDefinitionsLock";
    private const string MUTATION_GROUP_LOCK_MARKER_KEY = "Configuration.EfCore.MutationGroupLock";
    private const int PUBLISH_RETRY_BASE_DELAY_MS = 25;

    private readonly SemaphoreSlim _schemaInitializationLock = new(1, 1);
    private bool _schemaInitialized;

    private enum ConfigurationSchemaState
    {
        Missing,
        RequiresUpgrade,
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
    public async Task<ConfigurationMutationBatchCommitResult> CommitAsync(
        ConfigurationMutationBatchCommitRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        try
        {
            return await dbContextOperation.ExecuteAsync(async (strategyContext, token) =>
            {
                var strategy = strategyContext.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                    await dbContextOperation.ExecuteAsync(
                        (dbContext, innerToken) => CommitMutationBatchAttemptAsync(dbContext, request, innerToken),
                        token));
            }, cancellationToken);
        }
        catch
        {
            var committed = await TryGetCommittedMutationBatchAsync(request, cancellationToken);
            if (committed is not null)
            {
                return committed;
            }

            throw;
        }
    }

    private static async Task<ConfigurationMutationBatchCommitResult> CommitMutationBatchAttemptAsync(
        ConfigurationDbContext dbContext,
        ConfigurationMutationBatchCommitRequest request,
        CancellationToken cancellationToken)
    {
        var existingGroup = await dbContext.ConfigurationMutationGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.GroupId == request.MutationGroup.GroupId, cancellationToken);
        if (existingGroup is not null)
        {
            return await BuildCommittedMutationBatchResultAsync(dbContext, request, existingGroup, cancellationToken);
        }

        await EnsureLockMarkerExistsAsync(dbContext, MUTATION_GROUP_LOCK_MARKER_KEY, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            BuildStoreLockUpdateSql(dbContext.Database.ProviderName),
            [MUTATION_GROUP_LOCK_MARKER_KEY],
            cancellationToken);

        existingGroup = await dbContext.ConfigurationMutationGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.GroupId == request.MutationGroup.GroupId, cancellationToken);
        if (existingGroup is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await BuildCommittedMutationBatchResultAsync(dbContext, request, existingGroup, cancellationToken);
        }

        var definitionKeys = request.Items
            .Select(item => item.SaveRequest.Definition.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var entities = await dbContext.ConfigurationEffectiveValues
            .Where(value => definitionKeys.Contains(value.DefinitionKey))
            .ToDictionaryAsync(value => value.DefinitionKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in request.Items)
        {
            var save = item.SaveRequest;
            var definitionKey = save.Definition.DefinitionKey;
            entities.TryGetValue(definitionKey, out var entity);
            var currentVersion = entity?.Version ?? 0;
            if (save.ExpectedVersion is not null && currentVersion != save.ExpectedVersion)
            {
                throw new ConfigurationConcurrencyConflictException(
                    $"Expected version {save.ExpectedVersion} for '{definitionKey}', but current version is {currentVersion}.");
            }

            if (entity is null)
            {
                entity = new ConfigurationEffectiveValueEntity
                {
                    DefinitionKey = definitionKey
                };
                dbContext.ConfigurationEffectiveValues.Add(entity);
                entities[definitionKey] = entity;
            }

            var nextVersion = entity.Version + 1;
            if (item.History.Version != nextVersion)
            {
                throw new InvalidOperationException(
                    $"Prepared history version {item.History.Version} for '{definitionKey}' does not follow store version {entity.Version}.");
            }

            entity.Json = NormalizeJson(save.Json);
            entity.Version = nextVersion;
            entity.SchemaVersion = save.Definition.SchemaVersion;
            entity.LastModifiedTime = NormalizeUtcDateTime(item.History.ModifiedTime);
            entity.LastModifierId = save.Context.ModifierId;
            entity.LastModifierName = save.Context.ModifierName;
            dbContext.ConfigurationValueHistories.Add(ToEntity(item.History));
        }

        var groupEntity = new ConfigurationMutationGroupEntity
        {
            GroupId = request.MutationGroup.GroupId
        };
        ApplyMutationGroup(request.MutationGroup, groupEntity);
        dbContext.ConfigurationMutationGroups.Add(groupEntity);

        if (request.UnifiedVersion is not null)
        {
            var version = (await dbContext.ConfigurationUnifiedVersions
                .Select(candidate => (long?)candidate.Version)
                .MaxAsync(cancellationToken) ?? 0) + 1;
            var summary = CreateUnifiedVersionSummary(version, request.UnifiedVersion);
            dbContext.ConfigurationUnifiedVersions.Add(ToEntity(summary));
            dbContext.ConfigurationUnifiedVersionDocuments.AddRange(
                request.UnifiedVersion.Definitions.Select(definition => ToEntity(version, definition)));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConfigurationConcurrencyConflictException(
                "A configuration document changed while the mutation group was being committed.",
                ex);
        }

        return new ConfigurationMutationBatchCommitResult
        {
            MutationGroup = request.MutationGroup,
            AppliedRequestIds = request.Items.Select(static item => item.RequestId).ToArray(),
            Documents = entities.ToDictionary(
                static pair => pair.Key,
                static pair => ToDocument(pair.Value),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private async Task<ConfigurationMutationBatchCommitResult?> TryGetCommittedMutationBatchAsync(
        ConfigurationMutationBatchCommitRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var groupEntity = await dbContext.ConfigurationMutationGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(group => group.GroupId == request.MutationGroup.GroupId, token);
            return groupEntity is null
                ? null
                : await BuildCommittedMutationBatchResultAsync(dbContext, request, groupEntity, token);
        }, cancellationToken);
    }

    private static async Task<ConfigurationMutationBatchCommitResult> BuildCommittedMutationBatchResultAsync(
        ConfigurationDbContext dbContext,
        ConfigurationMutationBatchCommitRequest request,
        ConfigurationMutationGroupEntity groupEntity,
        CancellationToken cancellationToken)
    {
        var definitionKeys = request.Items
            .Select(item => item.SaveRequest.Definition.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var documents = await dbContext.ConfigurationEffectiveValues
            .AsNoTracking()
            .Where(value => definitionKeys.Contains(value.DefinitionKey))
            .ToDictionaryAsync(
                value => value.DefinitionKey,
                value => ToDocument(value),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        return new ConfigurationMutationBatchCommitResult
        {
            MutationGroup = ToGroup(groupEntity),
            AppliedRequestIds = request.Items.Select(static item => item.RequestId).ToArray(),
            Documents = documents
        };
    }

    private static ConfigurationUnifiedVersionSummary CreateUnifiedVersionSummary(
        long version,
        ConfigurationUnifiedVersionCreateRequest request)
    {
        return new ConfigurationUnifiedVersionSummary
        {
            Version = version,
            MutationGroupId = request.MutationGroupId,
            TriggerDefinitionKeys = NormalizeKeys(request.TriggerDefinitionKeys),
            DefinitionKeys = NormalizeKeys(request.Definitions.Select(static definition => definition.DefinitionKey)),
            DefinitionCount = request.Definitions.Count,
            CreatedTime = request.CreatedTime,
            ModifierId = request.ModifierId,
            ModifierName = request.ModifierName,
            Reason = request.Reason
        };
    }

    private static void ApplyMutationGroup(
        ConfigurationMutationGroup group,
        ConfigurationMutationGroupEntity entity)
    {
        entity.Label = group.Label;
        entity.Reason = group.Reason;
        entity.DefinitionKeysJson = JsonSerializer.Serialize(group.DefinitionKeys, ConfigurationPersistedJsonOptions.CompactValue);
        entity.MutationCount = group.MutationCount;
        entity.CreatedTime = NormalizeUtcDateTime(group.CreatedTime);
        entity.ModifierId = group.ModifierId;
        entity.ModifierName = group.ModifierName;
        entity.RolledBackTime = NormalizeNullableUtcDateTime(group.RolledBackTime);
        entity.RolledBackGroupId = group.RolledBackGroupId;
        entity.Status = group.Status.ToString();
    }

    /// <inheritdoc />
    public async Task<ConfigurationEffectiveValueDocument> EnsureCreatedAsync(
        ConfigurationDefinition definition,
        string seedJson,
        CancellationToken cancellationToken)
    {
        var documents = await EnsureCreatedAsync(
            [new ConfigurationEffectiveValueSeed { Definition = definition, SeedJson = seedJson }],
            cancellationToken);
        return documents[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationEffectiveValueDocument>> EnsureCreatedAsync(
        IReadOnlyList<ConfigurationEffectiveValueSeed> seeds,
        CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return [];
        }

        for (var attempt = 1; attempt <= MAX_EFFECTIVE_VALUE_ENSURE_RETRY_COUNT; attempt++)
        {
            try
            {
                return await ExecuteAsync(async (dbContext, token) =>
                {
                    var keys = seeds.Select(seed => seed.Definition.DefinitionKey).ToArray();
                    var existing = await dbContext.ConfigurationEffectiveValues
                        .Where(value => keys.Contains(value.DefinitionKey))
                        .ToDictionaryAsync(value => value.DefinitionKey, StringComparer.OrdinalIgnoreCase, token);

                    var documentsByKey = new Dictionary<string, ConfigurationEffectiveValueDocument>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entity in existing.Values)
                    {
                        documentsByKey[entity.DefinitionKey] = ToDocument(entity);
                    }

                    foreach (var seed in seeds)
                    {
                        if (documentsByKey.ContainsKey(seed.Definition.DefinitionKey))
                        {
                            continue;
                        }

                        var entity = new ConfigurationEffectiveValueEntity
                        {
                            DefinitionKey = seed.Definition.DefinitionKey,
                            Json = NormalizeJson(seed.SeedJson),
                            Version = 1,
                            SchemaVersion = seed.Definition.SchemaVersion,
                            LastModifiedTime = DateTime.UtcNow
                        };
                        dbContext.ConfigurationEffectiveValues.Add(entity);
                        documentsByKey[entity.DefinitionKey] = ToDocument(entity);
                    }

                    if (dbContext.ChangeTracker.HasChanges())
                    {
                        await dbContext.SaveChangesAsync(token);
                    }

                    return seeds
                        .Select(seed => documentsByKey[seed.Definition.DefinitionKey])
                        .ToArray();
                }, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MAX_EFFECTIVE_VALUE_ENSURE_RETRY_COUNT)
            {
                // Another instance may have inserted missing seed documents first.
            }
        }

        throw new InvalidOperationException(
            $"Failed to ensure {seeds.Count} configuration effective value documents after {MAX_EFFECTIVE_VALUE_ENSURE_RETRY_COUNT} attempts.");
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
            entity.LastModifiedTime = DateTime.UtcNow;
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
                var fromUtc = NormalizeUtcDateTime(from.Value);
                query = query.Where(history => history.ModifiedTime >= fromUtc);
            }

            if (to is not null)
            {
                var toUtc = NormalizeUtcDateTime(to.Value);
                query = query.Where(history => history.ModifiedTime <= toUtc);
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

            ApplyMutationGroup(group, entity);
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
                var fromUtc = NormalizeUtcDateTime(from.Value);
                query = query.Where(group => group.CreatedTime >= fromUtc);
            }

            if (to is not null)
            {
                var toUtc = NormalizeUtcDateTime(to.Value);
                query = query.Where(group => group.CreatedTime <= toUtc);
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
    public async Task<ConfigurationUnifiedVersionSnapshot> AppendVersionAsync(
        ConfigurationUnifiedVersionCreateRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MAX_UNIFIED_VERSION_APPEND_RETRY_COUNT; attempt++)
        {
            try
            {
                return await ExecuteAsync(async (dbContext, token) =>
                {
                    var version = (await dbContext.ConfigurationUnifiedVersions
                        .Select(candidate => (long?)candidate.Version)
                        .MaxAsync(token) ?? 0) + 1;
                    var summary = new ConfigurationUnifiedVersionSummary
                    {
                        Version = version,
                        MutationGroupId = request.MutationGroupId,
                        TriggerDefinitionKeys = NormalizeKeys(request.TriggerDefinitionKeys),
                        DefinitionKeys = NormalizeKeys(request.Definitions.Select(static definition => definition.DefinitionKey)),
                        DefinitionCount = request.Definitions.Count,
                        CreatedTime = request.CreatedTime,
                        ModifierId = request.ModifierId,
                        ModifierName = request.ModifierName,
                        Reason = request.Reason
                    };

                    dbContext.ConfigurationUnifiedVersions.Add(ToEntity(summary));
                    dbContext.ConfigurationUnifiedVersionDocuments.AddRange(
                        request.Definitions.Select(definition => ToEntity(version, definition)));
                    await dbContext.SaveChangesAsync(token);
                    return new ConfigurationUnifiedVersionSnapshot
                    {
                        Summary = summary,
                        Definitions = request.Definitions
                    };
                }, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MAX_UNIFIED_VERSION_APPEND_RETRY_COUNT)
            {
                // Another instance may have assigned the same next version first.
            }
        }

        throw new InvalidOperationException(
            $"Failed to append a unified configuration version after {MAX_UNIFIED_VERSION_APPEND_RETRY_COUNT} attempts.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var normalizedLimit = Math.Clamp(limit, 1, 500);
            var query = dbContext.ConfigurationUnifiedVersions.AsNoTracking();
            if (from is not null)
            {
                var fromUtc = NormalizeUtcDateTime(from.Value);
                query = query.Where(version => version.CreatedTime >= fromUtc);
            }

            if (to is not null)
            {
                var toUtc = NormalizeUtcDateTime(to.Value);
                query = query.Where(version => version.CreatedTime <= toUtc);
            }

            var summaries = (await query
                    .OrderByDescending(version => version.Version)
                    .ToArrayAsync(token))
                .Select(ToSummary)
                .ToArray();
            var filtered = string.IsNullOrWhiteSpace(definitionKey)
                ? summaries
                : summaries.Where(summary => summary.DefinitionKeys.Contains(definitionKey, StringComparer.OrdinalIgnoreCase));
            return filtered.Take(normalizedLimit).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(
        long version,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var summaryEntity = await dbContext.ConfigurationUnifiedVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Version == version, token);
            if (summaryEntity is null)
            {
                return null;
            }

            var documents = await dbContext.ConfigurationUnifiedVersionDocuments
                .AsNoTracking()
                .Where(document => document.Version == version)
                .OrderBy(document => document.DisplayName)
                .ThenBy(document => document.DefinitionKey)
                .ToArrayAsync(token);
            return new ConfigurationUnifiedVersionSnapshot
            {
                Summary = ToSummary(summaryEntity),
                Definitions = documents.Select(ToDefinitionSnapshot).ToArray()
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationUnifiedVersionSnapshot?> GetVersionByMutationGroupAsync(
        string mutationGroupId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async (dbContext, token) =>
        {
            var summaryEntity = await dbContext.ConfigurationUnifiedVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.MutationGroupId == mutationGroupId, token);
            if (summaryEntity is null)
            {
                return null;
            }

            var documents = await dbContext.ConfigurationUnifiedVersionDocuments
                .AsNoTracking()
                .Where(document => document.Version == summaryEntity.Version)
                .OrderBy(document => document.DisplayName)
                .ThenBy(document => document.DefinitionKey)
                .ToArrayAsync(token);
            return new ConfigurationUnifiedVersionSnapshot
            {
                Summary = ToSummary(summaryEntity),
                Definitions = documents.Select(ToDefinitionSnapshot).ToArray()
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken)
    {
        var candidates = definitions.Select(PublishedDefinitionCandidate.FromDefinition).ToArray();
        var changedCandidates = await GetChangedPublishCandidatesAsync(candidates, cancellationToken);
        if (changedCandidates.Count == 0)
        {
            return;
        }

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MAX_PUBLISH_RETRY_COUNT; attempt++)
        {
            try
            {
                await PublishCandidatesWithLockAsync(changedCandidates, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsPublishRetryableException(ex))
            {
                lastException = ex;
                if (attempt == MAX_PUBLISH_RETRY_COUNT)
                {
                    break;
                }

                await DelayPublishRetryAsync(attempt, cancellationToken);
                changedCandidates = await GetChangedPublishCandidatesAsync(candidates, cancellationToken);
                if (changedCandidates.Count == 0)
                {
                    return;
                }
            }
        }

        var diagnostics = await BuildPublishFailureDiagnosticsAsync(changedCandidates, cancellationToken);
        throw new InvalidOperationException(
            $"Failed to publish {changedCandidates.Count} configuration definition(s) after {MAX_PUBLISH_RETRY_COUNT} attempts. {diagnostics}",
            lastException);
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

    private async Task PublishCandidatesWithLockAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await dbContextOperation.ExecuteAsync(async (dbContext, token) =>
        {
            var executionStrategy = dbContext.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
                await EnsurePublishLockHeldAsync(dbContext, token);
                await PublishCandidatesAsync(dbContext, candidates, token);
                if (dbContext.ChangeTracker.HasChanges())
                {
                    await dbContext.SaveChangesAsync(token);
                }

                await transaction.CommitAsync(token);
            });
        }, cancellationToken);
    }

    private static async Task EnsurePublishLockHeldAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureLockMarkerExistsAsync(dbContext, PUBLISH_DEFINITIONS_LOCK_MARKER_KEY, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            BuildStoreLockUpdateSql(dbContext.Database.ProviderName),
            [PUBLISH_DEFINITIONS_LOCK_MARKER_KEY],
            cancellationToken);
    }

    private static async Task EnsureLockMarkerExistsAsync(
        ConfigurationDbContext dbContext,
        string markerKey,
        CancellationToken cancellationToken)
    {
        var lockMarker = await dbContext.ConfigurationSchemaMarkers
            .FirstOrDefaultAsync(candidate => candidate.MarkerKey == markerKey, cancellationToken);
        if (lockMarker is not null)
        {
            return;
        }

        dbContext.ConfigurationSchemaMarkers.Add(new ConfigurationSchemaMarkerEntity
        {
            MarkerKey = markerKey,
            SchemaVersion = ConfigurationSchemaMarkerEntity.CurrentSchemaVersion
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var markerCreatedByAnotherProcess = await dbContext.ConfigurationSchemaMarkers
                .AnyAsync(candidate => candidate.MarkerKey == markerKey, cancellationToken);
            if (markerCreatedByAnotherProcess)
            {
                return;
            }

            throw;
        }
    }

    private static async Task PublishCandidatesAsync(
        ConfigurationDbContext dbContext,
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        var keys = candidates.Select(candidate => candidate.DefinitionKey).ToArray();
        var existing = await dbContext.ConfigurationDefinitions
            .Where(definition => keys.Contains(definition.DefinitionKey))
            .ToDictionaryAsync(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!existing.TryGetValue(candidate.DefinitionKey, out var current))
            {
                var created = candidate.CreateEntity();
                dbContext.ConfigurationDefinitions.Add(created);
                dbContext.ConfigurationDefinitionPublishHistories.Add(
                    candidate.CreateHistory(null, ConfigurationDefinitionPublishChangeKind.Created));
                existing[created.DefinitionKey] = created;
                continue;
            }

            if (candidate.Matches(current))
            {
                continue;
            }

            var changeKind = candidate.HasSameSchema(current)
                ? ConfigurationDefinitionPublishChangeKind.MetadataChanged
                : ConfigurationDefinitionPublishChangeKind.SchemaChanged;
            var history = candidate.CreateHistory(current, changeKind);
            candidate.ApplyTo(current, history.NewSchemaVersion);
            dbContext.ConfigurationDefinitionPublishHistories.Add(history);
        }
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
                    await EnsureConfigurationSchemaMarkerAsync(dbContext, token);
                    return;
                }

                var schemaState = await GetConfigurationSchemaStateAsync(dbContext, token);
                if (schemaState == ConfigurationSchemaState.Missing)
                {
                    await creator.CreateTablesAsync(token);
                    await EnsureConfigurationSchemaMarkerAsync(dbContext, token);
                    return;
                }

                if (schemaState == ConfigurationSchemaState.RequiresUpgrade)
                {
                    await EnsureAdditiveSchemaAsync(dbContext, token);
                    await EnsureConfigurationSchemaMarkerAsync(dbContext, token);
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
                .OrderBy(definition => definition.DefinitionKey)
                .Select(definition => new
                {
                    definition.SchemaJson,
                    definition.FromProject,
                    definition.Category,
                    definition.Description,
                    definition.PublishRevision
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingTableException(ex))
        {
            return ConfigurationSchemaState.Missing;
        }
        catch (Exception ex) when (IsMissingColumnException(ex))
        {
            return ConfigurationSchemaState.RequiresUpgrade;
        }

        try
        {
            var marker = await dbContext.ConfigurationSchemaMarkers
                .AsNoTracking()
                .Where(candidate => candidate.MarkerKey == ConfigurationSchemaMarkerEntity.CurrentMarkerKey)
                .Select(candidate => new { candidate.SchemaVersion })
                .SingleOrDefaultAsync(cancellationToken);
            if (marker is null)
            {
                return ConfigurationSchemaState.RequiresUpgrade;
            }

            if (marker.SchemaVersion > ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"The Monica.Configuration database schema is version {marker.SchemaVersion}, but this runtime supports version {ConfigurationSchemaMarkerEntity.CurrentSchemaVersion}. " +
                    "Deploy a newer Monica.Configuration runtime instead of letting an older binary migrate or rewrite the schema.");
            }

            if (marker.SchemaVersion < ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
            {
                return ConfigurationSchemaState.RequiresUpgrade;
            }

            _ = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .OrderBy(history => history.HistoryId)
                .Select(history => new
                {
                    history.HistoryId,
                    history.DefinitionKey,
                    history.Description,
                    history.PublishedTime
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationUnifiedVersions
                .AsNoTracking()
                .OrderBy(version => version.Version)
                .Select(version => new
                {
                    version.Version,
                    version.DefinitionCount,
                    version.CreatedTime
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationUnifiedVersionDocuments
                .AsNoTracking()
                .OrderBy(document => document.Version)
                .ThenBy(document => document.DefinitionKey)
                .Select(document => new
                {
                    document.Version,
                    document.DefinitionKey,
                    document.SchemaHash
                })
                .FirstOrDefaultAsync(cancellationToken);
            return ConfigurationSchemaState.Ready;
        }
        catch (Exception ex) when (IsMissingTableException(ex) || IsMissingColumnException(ex))
        {
            return ConfigurationSchemaState.RequiresUpgrade;
        }
    }

    private static async Task EnsureAdditiveSchemaAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureConfigurationSchemaMarkerTableAsync(dbContext, cancellationToken);
        await EnsureUnifiedVersionTablesAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureConfigurationSchemaMarkerAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var marker = await dbContext.ConfigurationSchemaMarkers
            .FirstOrDefaultAsync(candidate => candidate.MarkerKey == ConfigurationSchemaMarkerEntity.CurrentMarkerKey, cancellationToken);
        if (marker is null)
        {
            dbContext.ConfigurationSchemaMarkers.Add(new ConfigurationSchemaMarkerEntity());
        }
        else if (marker.SchemaVersion > ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The Monica.Configuration database schema is version {marker.SchemaVersion}, but this runtime supports version {ConfigurationSchemaMarkerEntity.CurrentSchemaVersion}.");
        }
        else if (marker.SchemaVersion < ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            marker.SchemaVersion = ConfigurationSchemaMarkerEntity.CurrentSchemaVersion;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureConfigurationSchemaMarkerTableAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tableSql = FormatTableName(dbContext.Database.ProviderName, null, "ConfigurationSchemaMarkers");
        var sql = BuildCreateTableIfMissingSql(
            dbContext.Database.ProviderName,
            "ConfigurationSchemaMarkers",
            tableSql,
            BuildSchemaMarkerColumnsSql(dbContext.Database.ProviderName));
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureUnifiedVersionTablesAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var versionsTableSql = FormatTableName(providerName, null, "ConfigurationUnifiedVersions");
        var documentsTableSql = FormatTableName(providerName, null, "ConfigurationUnifiedVersionDocuments");

        await dbContext.Database.ExecuteSqlRawAsync(
            BuildCreateTableIfMissingSql(
                providerName,
                "ConfigurationUnifiedVersions",
                versionsTableSql,
                BuildUnifiedVersionsColumnsSql(providerName)),
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            BuildCreateTableIfMissingSql(
                providerName,
                "ConfigurationUnifiedVersionDocuments",
                documentsTableSql,
                BuildUnifiedVersionDocumentsColumnsSql(providerName)),
            cancellationToken);
    }

    private static string BuildCreateTableIfMissingSql(
        string? providerName,
        string tableName,
        string tableSql,
        string columnsSql)
    {
        if (providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true)
        {
            return $"""
                    IF OBJECT_ID(N'{tableName.Replace("'", "''", StringComparison.Ordinal)}', N'U') IS NULL
                    BEGIN
                        CREATE TABLE {tableSql} (
                            {columnsSql}
                        );
                    END
                    """;
        }

        return $"""
                CREATE TABLE IF NOT EXISTS {tableSql} (
                    {columnsSql}
                );
                """;
    }

    private static string BuildStoreLockUpdateSql(string? providerName)
    {
        var tableSql = FormatTableName(providerName, null, "ConfigurationSchemaMarkers");
        var markerKeySql = QuoteIdentifier(providerName, "MarkerKey");
        var schemaVersionSql = QuoteIdentifier(providerName, "SchemaVersion");
        return $"UPDATE {tableSql} SET {schemaVersionSql} = {schemaVersionSql} WHERE {markerKeySql} = {{0}}";
    }

    private static string BuildSchemaMarkerColumnsSql(string? providerName)
    {
        return providerName switch
        {
            var name when name?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true =>
                "[MarkerKey] nvarchar(100) NOT NULL, [SchemaVersion] int NOT NULL, CONSTRAINT [PK_ConfigurationSchemaMarkers] PRIMARY KEY ([MarkerKey])",
            var name when name?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true =>
                "`MarkerKey` varchar(100) NOT NULL, `SchemaVersion` int NOT NULL, PRIMARY KEY (`MarkerKey`)",
            _ =>
                "\"MarkerKey\" varchar(100) NOT NULL, \"SchemaVersion\" integer NOT NULL, PRIMARY KEY (\"MarkerKey\")"
        };
    }

    private static string BuildUnifiedVersionsColumnsSql(string? providerName)
    {
        return providerName switch
        {
            var name when name?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true =>
                "[Version] bigint NOT NULL, [MutationGroupId] nvarchar(450) NULL, [TriggerDefinitionKeysJson] nvarchar(max) NOT NULL, [DefinitionKeysJson] nvarchar(max) NOT NULL, [DefinitionCount] int NOT NULL, [CreatedTime] datetime2(6) NOT NULL, [ModifierId] nvarchar(max) NULL, [ModifierName] nvarchar(max) NULL, [Reason] nvarchar(max) NULL, CONSTRAINT [PK_ConfigurationUnifiedVersions] PRIMARY KEY ([Version])",
            var name when name?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true =>
                "`Version` bigint NOT NULL, `MutationGroupId` varchar(191) NULL, `TriggerDefinitionKeysJson` longtext NOT NULL, `DefinitionKeysJson` longtext NOT NULL, `DefinitionCount` int NOT NULL, `CreatedTime` datetime(6) NOT NULL, `ModifierId` longtext NULL, `ModifierName` longtext NULL, `Reason` longtext NULL, PRIMARY KEY (`Version`)",
            var name when name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) is true || name?.Contains("GaussDB", StringComparison.OrdinalIgnoreCase) is true =>
                "\"Version\" bigint NOT NULL, \"MutationGroupId\" text NULL, \"TriggerDefinitionKeysJson\" text NOT NULL, \"DefinitionKeysJson\" text NOT NULL, \"DefinitionCount\" integer NOT NULL, \"CreatedTime\" timestamp with time zone NOT NULL, \"ModifierId\" text NULL, \"ModifierName\" text NULL, \"Reason\" text NULL, PRIMARY KEY (\"Version\")",
            _ =>
                "\"Version\" integer NOT NULL, \"MutationGroupId\" text NULL, \"TriggerDefinitionKeysJson\" text NOT NULL, \"DefinitionKeysJson\" text NOT NULL, \"DefinitionCount\" integer NOT NULL, \"CreatedTime\" timestamp NOT NULL, \"ModifierId\" text NULL, \"ModifierName\" text NULL, \"Reason\" text NULL, PRIMARY KEY (\"Version\")"
        };
    }

    private static string BuildUnifiedVersionDocumentsColumnsSql(string? providerName)
    {
        return providerName switch
        {
            var name when name?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true =>
                "[Version] bigint NOT NULL, [DefinitionKey] nvarchar(450) NOT NULL, [DisplayName] nvarchar(max) NOT NULL, [Category] nvarchar(max) NULL, [FromProject] nvarchar(max) NOT NULL, [SchemaVersion] int NOT NULL, [SchemaHash] nvarchar(max) NOT NULL, [EffectiveValueVersion] bigint NULL, [Json] nvarchar(max) NOT NULL, [SourceContributionsJson] nvarchar(max) NOT NULL, CONSTRAINT [PK_ConfigurationUnifiedVersionDocuments] PRIMARY KEY ([Version], [DefinitionKey])",
            var name when name?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true =>
                "`Version` bigint NOT NULL, `DefinitionKey` varchar(191) NOT NULL, `DisplayName` longtext NOT NULL, `Category` longtext NULL, `FromProject` longtext NOT NULL, `SchemaVersion` int NOT NULL, `SchemaHash` longtext NOT NULL, `EffectiveValueVersion` bigint NULL, `Json` longtext NOT NULL, `SourceContributionsJson` longtext NOT NULL, PRIMARY KEY (`Version`, `DefinitionKey`)",
            _ =>
                "\"Version\" bigint NOT NULL, \"DefinitionKey\" text NOT NULL, \"DisplayName\" text NOT NULL, \"Category\" text NULL, \"FromProject\" text NOT NULL, \"SchemaVersion\" integer NOT NULL, \"SchemaHash\" text NOT NULL, \"EffectiveValueVersion\" bigint NULL, \"Json\" text NOT NULL, \"SourceContributionsJson\" text NOT NULL, PRIMARY KEY (\"Version\", \"DefinitionKey\")"
        };
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

    private static string QuoteIdentifier(string? providerName, string identifier)
    {
        if (providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true)
        {
            return QuoteSqlServer(identifier);
        }

        if (providerName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true)
        {
            return QuoteMySql(identifier);
        }

        return QuoteAnsi(identifier);
    }

    private static bool IsPublishRetryableException(Exception exception)
    {
        return ContainsException<DbUpdateException>(exception)
               || ContainsException<DbUpdateConcurrencyException>(exception);
    }

    private static bool ContainsException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException)
            {
                return true;
            }
        }

        return false;
    }

    private static Task DelayPublishRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(PUBLISH_RETRY_BASE_DELAY_MS * attempt + Random.Shared.Next(0, PUBLISH_RETRY_BASE_DELAY_MS));
        return Task.Delay(delay, cancellationToken);
    }

    private async Task<string> BuildPublishFailureDiagnosticsAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteAsync(async (dbContext, token) =>
            {
                var keys = candidates.Select(candidate => candidate.DefinitionKey).ToArray();
                var currentByKey = await dbContext.ConfigurationDefinitions
                    .AsNoTracking()
                    .Where(definition => keys.Contains(definition.DefinitionKey))
                    .ToDictionaryAsync(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase, token);
                var diagnostics = candidates
                    .Take(5)
                    .Select(candidate =>
                    {
                        currentByKey.TryGetValue(candidate.DefinitionKey, out var current);
                        var currentSchemaHash = current?.SchemaHash ?? "<missing>";
                        var currentFromProject = current?.FromProject ?? "<missing>";
                        var currentCategory = current?.Category ?? "<missing>";
                        return $"Definition='{candidate.DefinitionKey}', CurrentSchemaHash='{currentSchemaHash}', CandidateSchemaHash='{candidate.SchemaHash}', CurrentFromProject='{currentFromProject}', CandidateFromProject='{candidate.FromProject}', CurrentCategory='{currentCategory}', CandidateCategory='{candidate.Category ?? "<null>"}'";
                    });
                return string.Join("; ", diagnostics);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Failed to collect publish diagnostics: {ex.Message}";
        }
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

        public string? Description { get; init; }

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
                Description = NullIfWhiteSpace(definition.Description),
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
                   && string.Equals(Description, NullIfWhiteSpace(current.Description), StringComparison.Ordinal)
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
                Description = Description,
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
                PublishedTime = DateTime.UtcNow
            };
        }

        private void ApplySnapshot(ConfigurationDefinitionEntity entity, int schemaVersion)
        {
            entity.SectionPath = SectionPath;
            entity.DisplayName = DisplayName;
            entity.Description = Description;
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
                AddChange(changes, nameof(Description), NullIfWhiteSpace(current.Description), Description);
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
            LastModifiedTime = ToUtcOffset(entity.LastModifiedTime),
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
            entity.Description,
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
            Description = entity.Description,
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
            PublishedTime = ToUtcOffset(entity.PublishedTime)
        };
    }

    private static ConfigurationValueHistoryEntity ToEntity(ConfigurationValueHistory history)
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
            ModifiedTime = NormalizeUtcDateTime(history.ModifiedTime),
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
            ModifiedTime = ToUtcOffset(entity.ModifiedTime),
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
            CreatedTime = ToUtcOffset(entity.CreatedTime),
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            RolledBackTime = ToNullableUtcOffset(entity.RolledBackTime),
            RolledBackGroupId = entity.RolledBackGroupId,
            Status = Enum.Parse<ConfigurationMutationGroupStatus>(entity.Status)
        };
    }

    private static ConfigurationUnifiedVersionEntity ToEntity(ConfigurationUnifiedVersionSummary summary)
    {
        return new ConfigurationUnifiedVersionEntity
        {
            Version = summary.Version,
            MutationGroupId = summary.MutationGroupId,
            TriggerDefinitionKeysJson = JsonSerializer.Serialize(summary.TriggerDefinitionKeys, ConfigurationPersistedJsonOptions.CompactValue),
            DefinitionKeysJson = JsonSerializer.Serialize(summary.DefinitionKeys, ConfigurationPersistedJsonOptions.CompactValue),
            DefinitionCount = summary.DefinitionCount,
            CreatedTime = NormalizeUtcDateTime(summary.CreatedTime),
            ModifierId = summary.ModifierId,
            ModifierName = summary.ModifierName,
            Reason = summary.Reason
        };
    }

    private static ConfigurationUnifiedVersionSummary ToSummary(ConfigurationUnifiedVersionEntity entity)
    {
        return new ConfigurationUnifiedVersionSummary
        {
            Version = entity.Version,
            MutationGroupId = entity.MutationGroupId,
            TriggerDefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.TriggerDefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
            DefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.DefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
            DefinitionCount = entity.DefinitionCount,
            CreatedTime = ToUtcOffset(entity.CreatedTime),
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            Reason = entity.Reason
        };
    }

    private static ConfigurationUnifiedVersionDocumentEntity ToEntity(
        long version,
        ConfigurationUnifiedVersionDefinitionSnapshot definition)
    {
        return new ConfigurationUnifiedVersionDocumentEntity
        {
            Version = version,
            DefinitionKey = definition.DefinitionKey,
            DisplayName = definition.DisplayName,
            Category = definition.Category,
            FromProject = definition.FromProject,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            EffectiveValueVersion = definition.EffectiveValueVersion,
            Json = NormalizeJson(definition.Json),
            SourceContributionsJson = JsonSerializer.Serialize(
                definition.SourceContributions,
                ConfigurationPersistedJsonOptions.CompactValue)
        };
    }

    private static ConfigurationUnifiedVersionDefinitionSnapshot ToDefinitionSnapshot(
        ConfigurationUnifiedVersionDocumentEntity entity)
    {
        return new ConfigurationUnifiedVersionDefinitionSnapshot
        {
            DefinitionKey = entity.DefinitionKey,
            DisplayName = entity.DisplayName,
            Category = entity.Category,
            FromProject = entity.FromProject,
            SchemaVersion = entity.SchemaVersion,
            SchemaHash = entity.SchemaHash,
            EffectiveValueVersion = entity.EffectiveValueVersion,
            Json = entity.Json,
            SourceContributions = JsonSerializer.Deserialize<IReadOnlyList<ConfigurationUnifiedVersionSourceContribution>>(
                entity.SourceContributionsJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? []
        };
    }

    private static IReadOnlyList<string> NormalizeKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateTime NormalizeUtcDateTime(DateTimeOffset value)
    {
        return value.UtcDateTime;
    }

    private static DateTime? NormalizeNullableUtcDateTime(DateTimeOffset? value)
    {
        return value?.UtcDateTime;
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(NormalizeUtcDateTime(value));
    }

    private static DateTimeOffset? ToNullableUtcOffset(DateTime? value)
    {
        return value is null ? null : ToUtcOffset(value.Value);
    }

    private static DateTime NormalizeUtcDateTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
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
