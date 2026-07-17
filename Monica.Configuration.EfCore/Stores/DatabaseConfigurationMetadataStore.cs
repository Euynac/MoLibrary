using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.EfCore.Stores.Support;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores;

/// <summary>
/// Persists and reads published configuration definition metadata.
/// </summary>
internal sealed class DatabaseConfigurationMetadataStore(ConfigurationDatabase database)
    : IConfigurationMetadataStore
{
    private const int MAX_PUBLISH_RETRY_COUNT = 5;
    private const int PUBLISH_RETRY_BASE_DELAY_MS = 25;
    private const string PUBLISH_DEFINITIONS_LOCK_MARKER_KEY = "Configuration.EfCore.PublishDefinitionsLock";

    /// <inheritdoc />
    public ConfigurationStoreDescriptor Descriptor => ConfigurationDatabase.Descriptor;

    /// <inheritdoc />
    public async Task PublishAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var candidates = definitions.Select(PublishedDefinitionCandidate.FromDefinition).ToArray();
        if (!await HasPublishChangesAsync(candidates, cancellationToken))
        {
            return;
        }

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MAX_PUBLISH_RETRY_COUNT; attempt++)
        {
            try
            {
                // This precheck avoids an unnecessary distributed lock only. The transaction re-evaluates every
                // candidate because another publisher may have changed metadata after the precheck completed.
                await PublishCandidatesWithLockAsync(candidates, cancellationToken);
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
                if (!await HasPublishChangesAsync(candidates, cancellationToken))
                {
                    return;
                }
            }
        }

        var diagnostics = await BuildPublishFailureDiagnosticsAsync(candidates, cancellationToken);
        throw new InvalidOperationException(
            $"Failed to publish {candidates.Length} configuration definition(s) after {MAX_PUBLISH_RETRY_COUNT} attempts. {diagnostics}",
            lastException);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationPublishedDefinitionEntry>> ListPublishedDefinitionEntriesAsync(
        CancellationToken cancellationToken)
    {
        return await ExecuteMetadataReadAsync(async (dbContext, token) =>
        {
            var entities = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .OrderBy(definition => definition.DisplayName)
                .ThenBy(definition => definition.DefinitionKey)
                .ToArrayAsync(token);
            return ToPublishedDefinitionEntries(entities, token);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConfigurationPublishedDefinitionEntry?> GetPublishedDefinitionEntryAsync(
        string definitionKey,
        CancellationToken cancellationToken)
    {
        return await ExecuteMetadataReadAsync(async (dbContext, token) =>
        {
            var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
            var entities = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Where(definition => definition.DefinitionIdentity == definitionIdentity)
                .OrderBy(definition => definition.DefinitionKey)
                .Take(2)
                .ToArrayAsync(token);
            return ToPublishedDefinitionEntries(entities, token).FirstOrDefault();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationDefinitionPublishHistory>> ListDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var normalizedLimit = Math.Clamp(limit, 1, 200);
            var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
            var histories = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .Where(history => history.DefinitionIdentity == definitionIdentity)
                .OrderByDescending(history => history.PublishedTime)
                .ThenByDescending(history => history.HistoryId)
                .Take(normalizedLimit)
                .ToArrayAsync(token);

            return histories.Select(ConfigurationHistoryMapper.ToPublishHistory).ToArray();
        }, cancellationToken);
    }

    private async Task<bool> HasPublishChangesAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return false;
        }

        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var existing = await LoadExistingPublishedDefinitionsAsync(
                dbContext,
                candidates,
                trackChanges: false,
                token);

            return candidates.Any(candidate =>
                !existing.TryGetValue(candidate.DefinitionKey, out var current)
                || !candidate.Matches(current));
        }, cancellationToken);
    }

    private async Task PublishCandidatesWithLockAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        await database.ExecuteResilientAsync(async (dbContext, token) =>
        {
            // Marker creation performs SaveChanges and therefore must complete before the transaction it coordinates.
            await ConfigurationDatabaseLock.EnsureMarkerExistsAsync(
                dbContext,
                PUBLISH_DEFINITIONS_LOCK_MARKER_KEY,
                token);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            await ConfigurationDatabaseLock.AcquireAsync(
                dbContext,
                PUBLISH_DEFINITIONS_LOCK_MARKER_KEY,
                token);
            await PublishCandidatesAsync(dbContext, candidates, token);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(token);
            }

            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);
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

        var existing = await LoadExistingPublishedDefinitionsAsync(
            dbContext,
            candidates,
            trackChanges: true,
            cancellationToken);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private static async Task<Dictionary<string, ConfigurationDefinitionEntity>> LoadExistingPublishedDefinitionsAsync(
        ConfigurationDbContext dbContext,
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var duplicateCandidateKey = candidates
            .GroupBy(static candidate => candidate.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicateCandidateKey is not null)
        {
            throw CreateDuplicateDefinitionIdentityException(
                duplicateCandidateKey,
                "The current publisher supplied multiple definitions with the same case-insensitive key.");
        }

        var definitionIdentities = candidates
            .Select(static candidate => candidate.DefinitionIdentity)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        IQueryable<ConfigurationDefinitionEntity> query = dbContext.ConfigurationDefinitions;
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        var entities = await query
            .Where(definition => definitionIdentities.Contains(definition.DefinitionIdentity))
            .ToArrayAsync(cancellationToken);
        var duplicateStoredKey = entities
            .GroupBy(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicateStoredKey is not null)
        {
            throw CreateDuplicateDefinitionIdentityException(
                duplicateStoredKey,
                "The metadata database contains multiple rows with the same case-insensitive key.");
        }

        return entities.ToDictionary(
            static definition => definition.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ConfigurationDefinitionMetadataUnavailableException CreateDuplicateDefinitionIdentityException(
        string definitionKey,
        string message)
    {
        return new ConfigurationDefinitionMetadataUnavailableException(
            definitionKey,
            new ConfigurationDefinitionMetadataDiagnostic
            {
                Kind = ConfigurationDefinitionMetadataIssueKind.InvalidEnvelope,
                StoreKey = ConfigurationDatabase.Descriptor.StoreKey,
                ErrorType = typeof(InvalidDataException).FullName!,
                Message = message,
                RecommendedAction = ConfigurationDefinitionMetadataRepairAction.RepairMetadataStore
            });
    }

    private async Task<TResult> ExecuteMetadataReadAsync<TResult>(
        Func<ConfigurationDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await database.ExecuteAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex)
                                   || ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            throw new ConfigurationMetadataStoreReadException(
                ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                "The Monica.Configuration metadata database is missing required tables or columns. "
                + "Apply the current Monica.Configuration schema before reading or republishing definitions.",
                ex);
        }
    }

    private async Task<string> BuildPublishFailureDiagnosticsAsync(
        IReadOnlyList<PublishedDefinitionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        try
        {
            return await database.ExecuteAsync(async (dbContext, token) =>
            {
                var definitionIdentities = candidates
                    .Select(static candidate => candidate.DefinitionIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var currentByKey = await dbContext.ConfigurationDefinitions
                    .AsNoTracking()
                    .Where(definition => definitionIdentities.Contains(definition.DefinitionIdentity))
                    .ToDictionaryAsync(
                        definition => definition.DefinitionKey,
                        StringComparer.OrdinalIgnoreCase,
                        token);
                var diagnostics = candidates
                    .Take(5)
                    .Select(candidate =>
                    {
                        currentByKey.TryGetValue(candidate.DefinitionKey, out var current);
                        var currentSchemaHash = current?.SchemaHash ?? "<missing>";
                        var currentFromProject = current?.FromProject ?? "<missing>";
                        var currentCategory = current?.Category ?? "<missing>";
                        return $"Definition='{candidate.DefinitionKey}', CurrentSchemaHash='{currentSchemaHash}', "
                               + $"CandidateSchemaHash='{candidate.SchemaHash}', CurrentFromProject='{currentFromProject}', "
                               + $"CandidateFromProject='{candidate.FromProject}', CurrentCategory='{currentCategory}', "
                               + $"CandidateCategory='{candidate.Category ?? "<null>"}'";
                    });
                return string.Join("; ", diagnostics);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Failed to collect publish diagnostics: {ex.Message}";
        }
    }

    private static ConfigurationPublishedDefinitionEntry ToPublishedDefinitionEntry(
        ConfigurationDefinitionEntity entity)
    {
        return ConfigurationPublishedDefinitionEntry.Materialize(new ConfigurationPublishedDefinitionRecord
        {
            StoreKey = ConfigurationDatabase.Descriptor.StoreKey,
            DefinitionKey = entity.DefinitionKey,
            SectionPath = entity.SectionPath,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            ClrTypeName = entity.ClrTypeName,
            FromProject = entity.FromProject,
            Category = entity.Category,
            SchemaVersion = entity.SchemaVersion,
            SchemaHash = entity.SchemaHash,
            ReloadBehavior = entity.ReloadBehavior,
            SchemaJson = entity.SchemaJson
        });
    }

    private static IReadOnlyList<ConfigurationPublishedDefinitionEntry> ToPublishedDefinitionEntries(
        IReadOnlyList<ConfigurationDefinitionEntity> entities,
        CancellationToken cancellationToken)
    {
        var entries = new List<ConfigurationPublishedDefinitionEntry>(entities.Count);
        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(ToPublishedDefinitionEntry(entity));
        }

        var duplicateKeys = entries
            .GroupBy(static entry => entry.Metadata.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entries
            .Select(entry => duplicateKeys.Contains(entry.Metadata.DefinitionKey)
                ? ConfigurationPublishedDefinitionEntry.FromEnvelopeFailure(
                    entry.Metadata,
                    new InvalidDataException(
                        $"Multiple persisted definition metadata rows claim key '{entry.Metadata.DefinitionKey}'."),
                    ConfigurationDefinitionMetadataRepairAction.RepairMetadataStore)
                : entry)
            .ToArray();
    }

    private static bool IsPublishRetryableException(Exception exception)
    {
        return ContainsException<DbUpdateException>(exception);
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
        var jitter = Random.Shared.Next(0, PUBLISH_RETRY_BASE_DELAY_MS);
        var delay = TimeSpan.FromMilliseconds(PUBLISH_RETRY_BASE_DELAY_MS * attempt + jitter);
        return Task.Delay(delay, cancellationToken);
    }
}
