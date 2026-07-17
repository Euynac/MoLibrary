using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.EfCore.Stores.Support;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores;

internal sealed class DatabaseConfigurationUnifiedVersionStore(ConfigurationDatabase database)
    : IConfigurationUnifiedVersionStore
{
    public Task<ConfigurationUnifiedVersionSnapshot> AppendVersionAsync(
        ConfigurationUnifiedVersionCreateRequest request,
        CancellationToken cancellationToken)
    {
        // Appends are intentionally not retried as a whole: without a capture identity, an ambiguous successful
        // commit could otherwise create a second version. The database lock removes allocation races.
        return database.ExecuteAsync(async (dbContext, token) =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            // Append and delete use the same row lock, making version allocation and the current-version invariant serial.
            await ConfigurationDatabaseLock.AcquireAsync(
                dbContext,
                ConfigurationStoreLockEntity.UnifiedVersionsLockKey,
                token);

            var version = (await dbContext.ConfigurationUnifiedVersions
                .Select(candidate => (long?)candidate.Version)
                .MaxAsync(token) ?? 0) + 1;
            var summary = CreateSummary(version, request);

            dbContext.ConfigurationUnifiedVersions.Add(ConfigurationUnifiedVersionMapper.ToEntity(summary));
            dbContext.ConfigurationUnifiedVersionDocuments.AddRange(
                request.Definitions.Select(definition =>
                    ConfigurationUnifiedVersionMapper.ToEntity(version, definition)));
            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            return new ConfigurationUnifiedVersionSnapshot
            {
                Summary = summary,
                Definitions = request.Definitions
            };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return await database.ExecuteAsync(async (dbContext, token) =>
        {
            var query = dbContext.ConfigurationUnifiedVersions.AsNoTracking();
            if (from is not null)
            {
                var fromUtc = from.Value.UtcDateTime;
                query = query.Where(version => version.CreatedTime >= fromUtc);
            }

            if (to is not null)
            {
                var toUtc = to.Value.UtcDateTime;
                query = query.Where(version => version.CreatedTime <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(definitionKey))
            {
                var definitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
                query = query.Where(version => dbContext.ConfigurationUnifiedVersionDocuments.Any(document =>
                    document.Version == version.Version
                    && document.DefinitionIdentity == definitionIdentity));
            }

            var summaries = await query
                .OrderByDescending(version => version.Version)
                .Take(Math.Clamp(limit, 1, 500))
                .ToArrayAsync(token);
            return summaries.Select(ConfigurationUnifiedVersionMapper.ToSummary).ToArray();
        }, cancellationToken);
    }

    public Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(
        long version,
        CancellationToken cancellationToken)
    {
        return database.ExecuteAsync(
            (dbContext, token) => LoadSnapshotAsync(
                dbContext,
                dbContext.ConfigurationUnifiedVersions.Where(candidate => candidate.Version == version),
                token),
            cancellationToken);
    }

    public Task<ConfigurationUnifiedVersionSnapshot?> GetVersionByMutationGroupAsync(
        string mutationGroupId,
        CancellationToken cancellationToken)
    {
        return database.ExecuteAsync(
            (dbContext, token) => LoadSnapshotAsync(
                dbContext,
                dbContext.ConfigurationUnifiedVersions.Where(candidate =>
                    candidate.MutationGroupId == mutationGroupId),
                token),
            cancellationToken);
    }

    public async Task DeleteVersionAsync(long version, CancellationToken cancellationToken)
    {
        var versionWasObserved = false;
        _ = await database.ExecuteResilientAsync(async (dbContext, token) =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
            // The shared lock prevents an append from changing which version is current during this deletion.
            await ConfigurationDatabaseLock.AcquireAsync(
                dbContext,
                ConfigurationStoreLockEntity.UnifiedVersionsLockKey,
                token);

            var summary = await dbContext.ConfigurationUnifiedVersions
                .SingleOrDefaultAsync(candidate => candidate.Version == version, token);
            if (summary is null)
            {
                if (!versionWasObserved)
                {
                    throw new KeyNotFoundException($"Unified configuration version '{version}' was not found.");
                }

                // A retry after an ambiguous commit observes the already-completed deletion as success.
                await transaction.RollbackAsync(token);
                return true;
            }

            var latestVersion = await dbContext.ConfigurationUnifiedVersions
                .MaxAsync(candidate => candidate.Version, token);
            if (version == latestVersion)
            {
                throw new InvalidOperationException(
                    $"Unified configuration version '{version}' is the current version and cannot be deleted.");
            }

            versionWasObserved = true;
            var documents = await dbContext.ConfigurationUnifiedVersionDocuments
                .Where(document => document.Version == version)
                .ToArrayAsync(token);
            dbContext.ConfigurationUnifiedVersionDocuments.RemoveRange(documents);
            dbContext.ConfigurationUnifiedVersions.Remove(summary);
            await dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);
    }

    private static ConfigurationUnifiedVersionSummary CreateSummary(
        long version,
        ConfigurationUnifiedVersionCreateRequest request)
    {
        return new ConfigurationUnifiedVersionSummary
        {
            Version = version,
            MutationGroupId = request.MutationGroupId,
            TriggerDefinitionKeys = ConfigurationUnifiedVersionMapper.NormalizeKeys(request.TriggerDefinitionKeys),
            DefinitionKeys = ConfigurationUnifiedVersionMapper.NormalizeKeys(
                request.Definitions.Select(static definition => definition.DefinitionKey)),
            DefinitionCount = request.Definitions.Count,
            CreatedTime = request.CreatedTime,
            ModifierId = request.ModifierId,
            ModifierName = request.ModifierName,
            Reason = request.Reason
        };
    }

    private static async Task<ConfigurationUnifiedVersionSnapshot?> LoadSnapshotAsync(
        ConfigurationDbContext dbContext,
        IQueryable<ConfigurationUnifiedVersionEntity> summaryQuery,
        CancellationToken cancellationToken)
    {
        var summaryEntity = await summaryQuery
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (summaryEntity is null)
        {
            return null;
        }

        var documents = await dbContext.ConfigurationUnifiedVersionDocuments
            .AsNoTracking()
            .Where(document => document.Version == summaryEntity.Version)
            .OrderBy(document => document.DisplayName)
            .ThenBy(document => document.DefinitionKey)
            .ToArrayAsync(cancellationToken);
        return new ConfigurationUnifiedVersionSnapshot
        {
            Summary = ConfigurationUnifiedVersionMapper.ToSummary(summaryEntity),
            Definitions = documents.Select(ConfigurationUnifiedVersionMapper.ToDefinitionSnapshot).ToArray()
        };
    }
}
