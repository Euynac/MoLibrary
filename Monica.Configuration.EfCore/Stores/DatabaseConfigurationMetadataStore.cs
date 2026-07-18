using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.EfCore.Stores.Support;
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

    /// <inheritdoc />
    public ConfigurationStoreDescriptor Descriptor => ConfigurationDatabase.Descriptor;

    /// <inheritdoc />
    public async Task PublishAsync(
        ConfigurationDefinitionPublicationBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var diagnosticCandidates = batch.Publications
            .Select(static publication => PublishedDefinitionCandidate.FromPublication(
                publication,
                ConfigurationReloadBehaviorObservation.Aggregate([publication.ReloadBehaviorObservation])))
            .ToArray();

        Exception? lastException = null;
        for (var attempt = 1; attempt <= MAX_PUBLISH_RETRY_COUNT; attempt++)
        {
            try
            {
                await PublishBatchWithLockAsync(batch, cancellationToken);
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
            }
        }

        var diagnostics = await BuildPublishFailureDiagnosticsAsync(diagnosticCandidates, cancellationToken);
        throw new InvalidOperationException(
            $"Failed to publish {batch.Publications.Count} configuration definition(s) after {MAX_PUBLISH_RETRY_COUNT} attempts. {diagnostics}",
            lastException);
    }

    /// <inheritdoc />
    public Task RetirePublisherAsync(
        ConfigurationPublisherIdentity publisher,
        CancellationToken cancellationToken)
    {
        return PublishAsync(
            ConfigurationDefinitionPublicationBatch.Create(publisher, []),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationPublishedDefinitionEntry>> ListPublishedDefinitionEntriesAsync(
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
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
        return await database.ExecuteAsync(async (dbContext, token) =>
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
    public async Task<ConfigurationDefinitionPublicationOverview> GetDefinitionPublicationOverviewAsync(
        string definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var normalizedLimit = Math.Clamp(limit, 1, 200);
            var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
            var definition = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.DefinitionIdentity == definitionIdentity, token)
                ?? throw new KeyNotFoundException(
                    $"Published configuration definition '{definitionKey}' was not found.");
            var histories = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .Where(history => history.DefinitionIdentity == definitionIdentity)
                .OrderByDescending(history => history.DefinitionRevision)
                .ThenByDescending(history => history.HistoryId)
                .Take(normalizedLimit)
                .ToArrayAsync(token);
            var publisherStates = await dbContext.ConfigurationDefinitionPublisherStates
                .AsNoTracking()
                .Where(state => state.DefinitionIdentity == definitionIdentity)
                .OrderBy(state => state.PublisherKey)
                .ThenBy(state => state.PublisherIdentity)
                .ToArrayAsync(token);

            if (!Enum.TryParse<ConfigurationReloadBehavior>(
                    definition.ReloadBehavior,
                    ignoreCase: false,
                    out var reloadBehavior)
                || !Enum.IsDefined(reloadBehavior))
            {
                throw new InvalidDataException(
                    $"Published definition '{definition.DefinitionKey}' uses unsupported reload behavior "
                    + $"'{definition.ReloadBehavior}'.");
            }

            return new ConfigurationDefinitionPublicationOverview
            {
                DefinitionKey = definition.DefinitionKey,
                DefinitionRevision = definition.DefinitionRevision,
                SchemaVersion = definition.SchemaVersion,
                ReloadBehavior = reloadBehavior,
                PublisherStates = publisherStates
                    .Select(PublishedDefinitionPublisherStateCandidate.Materialize)
                    .ToArray(),
                RevisionHistories = histories
                    .Select(ConfigurationHistoryMapper.ToPublishHistory)
                    .ToArray()
            };
        }, cancellationToken);
    }

    private async Task PublishBatchWithLockAsync(
        ConfigurationDefinitionPublicationBatch batch,
        CancellationToken cancellationToken)
    {
        await database.ExecuteResilientAsync(async (dbContext, token) =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            await ConfigurationDatabaseLock.AcquireAsync(
                dbContext,
                ConfigurationStoreLockEntity.DefinitionPublicationLockKey,
                token);
            await PublishBatchAsync(dbContext, batch, token);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(token);
            }

            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);
    }

    private static async Task PublishBatchAsync(
        ConfigurationDbContext dbContext,
        ConfigurationDefinitionPublicationBatch batch,
        CancellationToken cancellationToken)
    {
        var stateCandidates = batch.Publications
            .Select(publication => PublishedDefinitionPublisherStateCandidate.FromPublication(
                batch.Publisher,
                publication))
            .ToArray();
        var incomingIdentities = stateCandidates
            .Select(static candidate => candidate.DefinitionIdentity)
            .ToHashSet(StringComparer.Ordinal);
        var publisherIdentity = PublishedDefinitionPublisherIdentity.Compute(batch.Publisher.PublisherKey);
        var currentPublisherStates = await dbContext.ConfigurationDefinitionPublisherStates
            .Where(state => state.PublisherIdentity == publisherIdentity)
            .ToArrayAsync(cancellationToken);
        var affectedIdentities = currentPublisherStates
            .Select(static state => state.DefinitionIdentity)
            .Concat(incomingIdentities)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (affectedIdentities.Length == 0)
        {
            return;
        }

        var publisherStates = await dbContext.ConfigurationDefinitionPublisherStates
            .Where(state => affectedIdentities.Contains(state.DefinitionIdentity))
            .ToArrayAsync(cancellationToken);
        var statesByPublisher = publisherStates.ToDictionary(
            static state => (state.DefinitionIdentity, state.PublisherIdentity));

        // A publication is a complete logical-service snapshot. Missing rows therefore withdraw that publisher's
        // earlier evidence, while observations from other services remain active until explicitly retired.
        foreach (var staleState in currentPublisherStates
                     .Where(state => !incomingIdentities.Contains(state.DefinitionIdentity)))
        {
            dbContext.ConfigurationDefinitionPublisherStates.Remove(staleState);
        }

        foreach (var candidate in stateCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (candidate.DefinitionIdentity, candidate.PublisherIdentity);
            if (statesByPublisher.TryGetValue(key, out var currentState))
            {
                candidate.ApplyTo(currentState);
                continue;
            }

            var createdState = candidate.CreateEntity();
            dbContext.ConfigurationDefinitionPublisherStates.Add(createdState);
            statesByPublisher[key] = createdState;
        }

        var currentDefinitions = await dbContext.ConfigurationDefinitions
            .Where(definition => affectedIdentities.Contains(definition.DefinitionIdentity))
            .ToArrayAsync(cancellationToken);
        var definitionsByIdentity = currentDefinitions.ToDictionary(
            static definition => definition.DefinitionIdentity,
            StringComparer.Ordinal);
        var publicationsByIdentity = batch.Publications.ToDictionary(
            publication => ConfigurationDefinitionIdentity.Compute(publication.Definition.DefinitionKey),
            StringComparer.Ordinal);

        // Aggregate after tracked additions and removals so the canonical definition changes only when the
        // cross-service result changes, not whenever a different service happens to publish last.
        foreach (var definitionIdentity in affectedIdentities.OrderBy(
                     static identity => identity,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effectiveReloadBehavior = ConfigurationReloadBehaviorObservation.Aggregate(
                statesByPublisher.Values
                    .Where(state =>
                        string.Equals(state.DefinitionIdentity, definitionIdentity, StringComparison.Ordinal)
                        && dbContext.Entry(state).State != EntityState.Deleted)
                    .Select(PublishedDefinitionPublisherStateCandidate.MaterializeObservation));

            definitionsByIdentity.TryGetValue(definitionIdentity, out var current);
            PublishedDefinitionCandidate candidate;
            if (publicationsByIdentity.TryGetValue(definitionIdentity, out var publication))
            {
                candidate = PublishedDefinitionCandidate.FromPublication(publication, effectiveReloadBehavior);
            }
            else if (current is not null)
            {
                candidate = PublishedDefinitionCandidate.FromCurrent(current, effectiveReloadBehavior);
            }
            else
            {
                // An orphaned observation cannot materialize a trustworthy definition envelope.
                continue;
            }

            if (current is null)
            {
                const int initialDefinitionRevision = 1;
                var initialSchemaVersion = candidate.ResolveNewSchemaVersion(
                    null,
                    ConfigurationDefinitionPublishChangeKind.Created);
                var created = candidate.CreateEntity(initialSchemaVersion, initialDefinitionRevision);
                dbContext.ConfigurationDefinitions.Add(created);
                dbContext.ConfigurationDefinitionPublishHistories.Add(
                    candidate.CreateHistory(
                        null,
                        ConfigurationDefinitionPublishChangeKind.Created,
                        batch.Publisher,
                        initialSchemaVersion,
                        initialDefinitionRevision));
                definitionsByIdentity[created.DefinitionIdentity] = created;
                continue;
            }

            if (candidate.Matches(current))
            {
                continue;
            }

            var changeKind = candidate.HasSameSchema(current)
                ? ConfigurationDefinitionPublishChangeKind.MetadataChanged
                : ConfigurationDefinitionPublishChangeKind.SchemaChanged;
            var schemaVersion = candidate.ResolveNewSchemaVersion(current, changeKind);
            var definitionRevision = checked(Math.Max(current.DefinitionRevision, 0) + 1);
            var history = candidate.CreateHistory(
                current,
                changeKind,
                batch.Publisher,
                schemaVersion,
                definitionRevision);
            candidate.ApplyTo(current, schemaVersion, definitionRevision);
            dbContext.ConfigurationDefinitionPublishHistories.Add(history);
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
            DefinitionRevision = entity.DefinitionRevision,
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
