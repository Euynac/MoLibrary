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
    private const int MAX_APPEND_RETRY_COUNT = 5;

    public async Task<ConfigurationUnifiedVersionSnapshot> AppendVersionAsync(
        ConfigurationUnifiedVersionCreateRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MAX_APPEND_RETRY_COUNT; attempt++)
        {
            try
            {
                return await database.ExecuteAsync(async (dbContext, token) =>
                {
                    var version = (await dbContext.ConfigurationUnifiedVersions
                        .Select(candidate => (long?)candidate.Version)
                        .MaxAsync(token) ?? 0) + 1;
                    var summary = CreateSummary(version, request);

                    dbContext.ConfigurationUnifiedVersions.Add(ConfigurationUnifiedVersionMapper.ToEntity(summary));
                    dbContext.ConfigurationUnifiedVersionDocuments.AddRange(
                        request.Definitions.Select(definition =>
                            ConfigurationUnifiedVersionMapper.ToEntity(version, definition)));
                    await dbContext.SaveChangesAsync(token);

                    return new ConfigurationUnifiedVersionSnapshot
                    {
                        Summary = summary,
                        Definitions = request.Definitions
                    };
                }, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MAX_APPEND_RETRY_COUNT)
            {
                // Another instance may have assigned the same next version first.
            }
        }

        throw new InvalidOperationException(
            $"Failed to append a unified configuration version after {MAX_APPEND_RETRY_COUNT} attempts.");
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
