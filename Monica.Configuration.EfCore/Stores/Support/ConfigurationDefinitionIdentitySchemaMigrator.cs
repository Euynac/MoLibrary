using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDefinitionIdentitySchemaMigrator
{
    private const int DEFINITION_IDENTITY_BACKFILL_BATCH_SIZE = 500;

    internal static async Task EnsureSchemaAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var indexes = GetDefinitionIdentityIndexes();
        foreach (var index in indexes)
        {
            await EnsureDefinitionIdentityColumnAsync(dbContext, index.TableName, cancellationToken);
        }

        // Recreate known indexes after the column shape is finalized so interrupted and concurrent upgrades are safe.
        await DropDefinitionIdentityIndexesAsync(dbContext, indexes, cancellationToken);

        foreach (var tableName in indexes.Select(static index => index.TableName))
        {
            await PrepareDefinitionIdentityBackfillAsync(dbContext, tableName, cancellationToken);
        }

        await BackfillDefinitionIdentitiesAsync(
            dbContext,
            dbContext.ConfigurationDefinitions,
            static query => query.OrderBy(static entity => entity.DefinitionKey),
            static entity => entity.DefinitionKey,
            static entity => entity.DefinitionIdentity,
            static (entity, identity) => entity.DefinitionIdentity = identity,
            "ConfigurationDefinitions",
            cancellationToken);
        await BackfillDefinitionIdentitiesAsync(
            dbContext,
            dbContext.ConfigurationDefinitionPublishHistories,
            static query => query.OrderBy(static entity => entity.HistoryId),
            static entity => entity.DefinitionKey,
            static entity => entity.DefinitionIdentity,
            static (entity, identity) => entity.DefinitionIdentity = identity,
            "ConfigurationDefinitionPublishHistories",
            cancellationToken);
        await BackfillDefinitionIdentitiesAsync(
            dbContext,
            dbContext.ConfigurationEffectiveValues,
            static query => query.OrderBy(static entity => entity.DefinitionKey),
            static entity => entity.DefinitionKey,
            static entity => entity.DefinitionIdentity,
            static (entity, identity) => entity.DefinitionIdentity = identity,
            "ConfigurationEffectiveValues",
            cancellationToken);
        await BackfillDefinitionIdentitiesAsync(
            dbContext,
            dbContext.ConfigurationValueHistories,
            static query => query.OrderBy(static entity => entity.HistoryId),
            static entity => entity.DefinitionKey,
            static entity => entity.DefinitionIdentity,
            static (entity, identity) => entity.DefinitionIdentity = identity,
            "ConfigurationValueHistories",
            cancellationToken);
        await BackfillDefinitionIdentitiesAsync(
            dbContext,
            dbContext.ConfigurationUnifiedVersionDocuments,
            static query => query
                .OrderBy(static entity => entity.Version)
                .ThenBy(static entity => entity.DefinitionKey),
            static entity => entity.DefinitionKey,
            static entity => entity.DefinitionIdentity,
            static (entity, identity) => entity.DefinitionIdentity = identity,
            "ConfigurationUnifiedVersionDocuments",
            cancellationToken);

        await EnsureDefinitionIdentityUniquenessAsync(dbContext, cancellationToken);

        foreach (var index in indexes)
        {
            await EnsureDefinitionIdentityIsRequiredAsync(dbContext, index, cancellationToken);
        }

        await EnsureDefinitionIdentityIndexesAsync(dbContext, indexes, cancellationToken);
    }

    private static async Task EnsureDefinitionIdentityColumnAsync(
        ConfigurationDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, tableName);
        var columnSql = ConfigurationDatabaseSql.QuoteIdentifier(
            providerName,
            nameof(ConfigurationDefinitionEntity.DefinitionIdentity));
        var columnConstraint = ConfigurationDatabaseSql.IsSqlite(providerName)
            ? "NOT NULL DEFAULT ''"
            : "NULL";
        var addColumnSql =
            $"ALTER TABLE {tableSql} ADD {columnSql} {ConfigurationDatabaseSql.DefinitionIdentityColumnType} {columnConstraint}";

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(addColumnSql, cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsDuplicateColumn(ex))
        {
            // A previous runtime or another replica already added this column.
        }
    }

    private static async Task BackfillDefinitionIdentitiesAsync<TEntity>(
        ConfigurationDbContext dbContext,
        DbSet<TEntity> entities,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderEntities,
        Func<TEntity, string> getDefinitionKey,
        Func<TEntity, string?> getDefinitionIdentity,
        Action<TEntity, string> setDefinitionIdentity,
        string tableName,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var processedCount = 0;
        while (true)
        {
            var batch = await orderEntities(entities)
                .Skip(processedCount)
                .Take(DEFINITION_IDENTITY_BACKFILL_BATCH_SIZE)
                .ToArrayAsync(cancellationToken);
            if (batch.Length == 0)
            {
                return;
            }

            foreach (var entity in batch)
            {
                var definitionKey = getDefinitionKey(entity);
                try
                {
                    var expectedIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey);
                    if (!string.Equals(getDefinitionIdentity(entity), expectedIdentity, StringComparison.Ordinal))
                    {
                        setDefinitionIdentity(entity, expectedIdentity);
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new ConfigurationMetadataStoreReadException(
                        ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                        $"The Monica.Configuration schema upgrade cannot normalize definition key '{definitionKey}' "
                        + $"in table '{tableName}'. Repair or remove this persisted row, then retry the upgrade.",
                        ex);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            processedCount += batch.Length;
        }
    }

    private static async Task PrepareDefinitionIdentityBackfillAsync(
        ConfigurationDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, tableName);
        var columnSql = ConfigurationDatabaseSql.QuoteIdentifier(
            providerName,
            nameof(ConfigurationDefinitionEntity.DefinitionIdentity));
        var sql = $"UPDATE {tableSql} SET {columnSql} = {{0}} WHERE {columnSql} IS NULL";

        // EF materializes this property as a required string. Give legacy NULLs a sentinel before tracked backfill.
        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [new string('0', ConfigurationDefinitionIdentity.Length)],
            cancellationToken);
    }

    private static async Task EnsureDefinitionIdentityUniquenessAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var duplicateDefinitionIdentity = await dbContext.ConfigurationDefinitions
            .AsNoTracking()
            .GroupBy(static entity => entity.DefinitionIdentity)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicateDefinitionIdentity is not null)
        {
            var keys = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Where(entity => entity.DefinitionIdentity == duplicateDefinitionIdentity)
                .Select(static entity => entity.DefinitionKey)
                .OrderBy(static key => key)
                .ToArrayAsync(cancellationToken);
            ThrowDefinitionIdentityCollision(
                "ConfigurationDefinitions",
                duplicateDefinitionIdentity,
                keys);
        }

        var duplicateEffectiveValueIdentity = await dbContext.ConfigurationEffectiveValues
            .AsNoTracking()
            .GroupBy(static entity => entity.DefinitionIdentity)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicateEffectiveValueIdentity is not null)
        {
            var keys = await dbContext.ConfigurationEffectiveValues
                .AsNoTracking()
                .Where(entity => entity.DefinitionIdentity == duplicateEffectiveValueIdentity)
                .Select(static entity => entity.DefinitionKey)
                .OrderBy(static key => key)
                .ToArrayAsync(cancellationToken);
            ThrowDefinitionIdentityCollision(
                "ConfigurationEffectiveValues",
                duplicateEffectiveValueIdentity,
                keys);
        }

        var duplicateVersionDocument = await dbContext.ConfigurationUnifiedVersionDocuments
            .AsNoTracking()
            .GroupBy(static entity => new { entity.Version, entity.DefinitionIdentity })
            .Where(static group => group.Count() > 1)
            .Select(static group => new { group.Key.Version, group.Key.DefinitionIdentity })
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicateVersionDocument is null)
        {
            return;
        }

        var duplicateVersionKeys = await dbContext.ConfigurationUnifiedVersionDocuments
            .AsNoTracking()
            .Where(entity => entity.Version == duplicateVersionDocument.Version
                             && entity.DefinitionIdentity == duplicateVersionDocument.DefinitionIdentity)
            .Select(static entity => entity.DefinitionKey)
            .OrderBy(static key => key)
            .ToArrayAsync(cancellationToken);
        ThrowDefinitionIdentityCollision(
            $"ConfigurationUnifiedVersionDocuments version {duplicateVersionDocument.Version}",
            duplicateVersionDocument.DefinitionIdentity,
            duplicateVersionKeys);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowDefinitionIdentityCollision(
        string location,
        string definitionIdentity,
        IReadOnlyList<string> definitionKeys)
    {
        throw new ConfigurationMetadataStoreReadException(
            ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
            $"The Monica.Configuration schema upgrade found multiple live rows in '{location}' with the "
            + $"case-insensitive identity '{definitionIdentity}': {string.Join(", ", definitionKeys.Select(static key => $"'{key}'"))}. "
            + "Merge or remove the duplicate rows, then retry the upgrade.");
    }

    private static async Task EnsureDefinitionIdentityIsRequiredAsync(
        ConfigurationDbContext dbContext,
        DefinitionIdentityIndex index,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        if (ConfigurationDatabaseSql.IsSqlite(providerName))
        {
            await EnsureSqliteDefinitionIdentityTriggersAsync(dbContext, index, cancellationToken);
            return;
        }

        var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, index.TableName);
        var columnSql = ConfigurationDatabaseSql.QuoteIdentifier(
            providerName,
            nameof(ConfigurationDefinitionEntity.DefinitionIdentity));
        var sql = providerName switch
        {
            var name when ConfigurationDatabaseSql.IsSqlServer(name) =>
                $"ALTER TABLE {tableSql} ALTER COLUMN {columnSql} {ConfigurationDatabaseSql.DefinitionIdentityColumnType} NOT NULL",
            var name when ConfigurationDatabaseSql.IsMySql(name) =>
                $"ALTER TABLE {tableSql} MODIFY COLUMN {columnSql} {ConfigurationDatabaseSql.DefinitionIdentityColumnType} NOT NULL",
            _ =>
                $"ALTER TABLE {tableSql} ALTER COLUMN {columnSql} TYPE {ConfigurationDatabaseSql.DefinitionIdentityColumnType}, "
                + $"ALTER COLUMN {columnSql} SET NOT NULL"
        };
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureSqliteDefinitionIdentityTriggersAsync(
        ConfigurationDbContext dbContext,
        DefinitionIdentityIndex index,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, index.TableName);
        var columnSql = ConfigurationDatabaseSql.QuoteIdentifier(
            providerName,
            nameof(ConfigurationDefinitionEntity.DefinitionIdentity));
        var triggerPrefix = $"TRG_{index.IndexName}";
        var invalidIdentitySql =
            $"NEW.{columnSql} IS NULL OR length(NEW.{columnSql}) <> {ConfigurationDefinitionIdentity.Length} "
            + $"OR NEW.{columnSql} GLOB '*[^0-9A-F]*'";
        var errorMessage = $"Invalid definition identity in {index.TableName}.";
        var insertTriggerSql =
            $"CREATE TRIGGER IF NOT EXISTS {ConfigurationDatabaseSql.QuoteAnsi(triggerPrefix + "_I")} "
            + $"BEFORE INSERT ON {tableSql} WHEN {invalidIdentitySql} "
            + $"BEGIN SELECT RAISE(ABORT, '{errorMessage}'); END";
        var updateTriggerSql =
            $"CREATE TRIGGER IF NOT EXISTS {ConfigurationDatabaseSql.QuoteAnsi(triggerPrefix + "_U")} "
            + $"BEFORE UPDATE OF {columnSql} ON {tableSql} WHEN {invalidIdentitySql} "
            + $"BEGIN SELECT RAISE(ABORT, '{errorMessage}'); END";

        await dbContext.Database.ExecuteSqlRawAsync(insertTriggerSql, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(updateTriggerSql, cancellationToken);
    }

    private static async Task EnsureDefinitionIdentityIndexesAsync(
        ConfigurationDbContext dbContext,
        IReadOnlyList<DefinitionIdentityIndex> indexes,
        CancellationToken cancellationToken)
    {
        foreach (var index in indexes)
        {
            var sql = BuildCreateIndexIfMissingSql(dbContext.Database.ProviderName, index);
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsDuplicateIndex(ex))
            {
                // Another replica completed the same schema upgrade concurrently.
            }
            catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsUniqueConstraintViolation(ex))
            {
                throw new ConfigurationMetadataStoreReadException(
                    ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                    $"The Monica.Configuration schema upgrade could not create identity index '{index.IndexName}' "
                    + "because conflicting rows were written during the upgrade. Stop older replicas, repair duplicate "
                    + "case-insensitive definition keys, and retry.",
                    ex);
            }
        }
    }

    private static IReadOnlyList<DefinitionIdentityIndex> GetDefinitionIdentityIndexes()
    {
        return
        [
            new DefinitionIdentityIndex(
                "ConfigurationDefinitions",
                ConfigurationDbSchema.DefinitionIdentityIndex,
                [nameof(ConfigurationDefinitionEntity.DefinitionIdentity)],
                IsUnique: true),
            new DefinitionIdentityIndex(
                "ConfigurationDefinitionPublishHistories",
                ConfigurationDbSchema.DefinitionPublishIdentityIndex,
                [
                    nameof(ConfigurationDefinitionPublishHistoryEntity.DefinitionIdentity),
                    nameof(ConfigurationDefinitionPublishHistoryEntity.PublishedTime)
                ],
                IsUnique: false),
            new DefinitionIdentityIndex(
                "ConfigurationEffectiveValues",
                ConfigurationDbSchema.EffectiveValueIdentityIndex,
                [nameof(ConfigurationEffectiveValueEntity.DefinitionIdentity)],
                IsUnique: true),
            new DefinitionIdentityIndex(
                "ConfigurationValueHistories",
                ConfigurationDbSchema.ValueHistoryIdentityIndex,
                [
                    nameof(ConfigurationValueHistoryEntity.DefinitionIdentity),
                    nameof(ConfigurationValueHistoryEntity.PathDepth),
                    nameof(ConfigurationValueHistoryEntity.ModifiedTime)
                ],
                IsUnique: false),
            new DefinitionIdentityIndex(
                "ConfigurationUnifiedVersionDocuments",
                ConfigurationDbSchema.UnifiedVersionDocumentIdentityIndex,
                [
                    nameof(ConfigurationUnifiedVersionDocumentEntity.DefinitionIdentity),
                    nameof(ConfigurationUnifiedVersionDocumentEntity.Version)
                ],
                IsUnique: true)
        ];
    }

    private static async Task DropDefinitionIdentityIndexesAsync(
        ConfigurationDbContext dbContext,
        IReadOnlyList<DefinitionIdentityIndex> currentIndexes,
        CancellationToken cancellationToken)
    {
        DefinitionIdentityIndex[] legacyIndexes =
        [
            new(
                "ConfigurationDefinitions",
                "IX_ConfigurationDefinitions_DefinitionIdentity",
                [],
                IsUnique: true),
            new(
                "ConfigurationDefinitionPublishHistories",
                "IX_ConfigurationDefinitionPublishHistories_DefinitionIdentity_PublishedTime",
                [],
                IsUnique: false),
            new(
                "ConfigurationEffectiveValues",
                "IX_ConfigurationEffectiveValues_DefinitionIdentity",
                [],
                IsUnique: true),
            new(
                "ConfigurationValueHistories",
                "IX_ConfigurationValueHistories_DefinitionIdentity_PathDepth_ModifiedTime",
                [],
                IsUnique: false),
            new(
                "ConfigurationUnifiedVersionDocuments",
                "IX_ConfigurationUnifiedVersionDocuments_DefinitionIdentity_Version",
                [],
                IsUnique: true)
        ];

        foreach (var index in currentIndexes.Concat(legacyIndexes))
        {
            var providerName = dbContext.Database.ProviderName;
            if (ConfigurationDatabaseSql.IsMySql(providerName) && index.IndexName.Length > 64)
            {
                // MySQL rejects these legacy names before it can resolve them; they could never have been created.
                continue;
            }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    BuildDropIndexIfExistsSql(providerName, index),
                    cancellationToken);
            }
            catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingIndex(ex))
            {
                // MySQL lacks DROP INDEX IF EXISTS. A missing index is the desired state.
            }
        }
    }

    private static string BuildDropIndexIfExistsSql(
        string? providerName,
        DefinitionIdentityIndex index)
    {
        var indexSql = ConfigurationDatabaseSql.QuoteIdentifier(providerName, index.IndexName);
        if (ConfigurationDatabaseSql.IsSqlServer(providerName))
        {
            var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, index.TableName);
            return $"DROP INDEX IF EXISTS {indexSql} ON {tableSql}";
        }

        if (ConfigurationDatabaseSql.IsMySql(providerName))
        {
            var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, index.TableName);
            return $"DROP INDEX {indexSql} ON {tableSql}";
        }

        return $"DROP INDEX IF EXISTS {indexSql}";
    }

    private static string BuildCreateIndexIfMissingSql(
        string? providerName,
        DefinitionIdentityIndex index)
    {
        var tableSql = ConfigurationDatabaseSql.FormatTableName(providerName, null, index.TableName);
        var indexSql = ConfigurationDatabaseSql.QuoteIdentifier(providerName, index.IndexName);
        var columnsSql = string.Join(
            ", ",
            index.ColumnNames.Select(column => ConfigurationDatabaseSql.QuoteIdentifier(providerName, column)));
        var uniqueSql = index.IsUnique ? "UNIQUE " : string.Empty;
        var createSql = $"CREATE {uniqueSql}INDEX {indexSql} ON {tableSql} ({columnsSql})";

        if (ConfigurationDatabaseSql.IsSqlServer(providerName))
        {
            var escapedIndexName = index.IndexName.Replace("'", "''", StringComparison.Ordinal);
            var escapedTableName = index.TableName.Replace("'", "''", StringComparison.Ordinal);
            return $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{escapedIndexName}' AND object_id = OBJECT_ID(N'{escapedTableName}')) {createSql}";
        }

        return ConfigurationDatabaseSql.IsMySql(providerName)
            ? createSql
            : createSql.Replace("INDEX ", "INDEX IF NOT EXISTS ", StringComparison.Ordinal);
    }

    private sealed record DefinitionIdentityIndex(
        string TableName,
        string IndexName,
        IReadOnlyList<string> ColumnNames,
        bool IsUnique);
}
