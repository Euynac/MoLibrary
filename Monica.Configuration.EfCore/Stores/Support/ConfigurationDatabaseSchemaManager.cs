using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Modules;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Stores.Support;

internal sealed class ConfigurationDatabaseSchemaManager(
    IDbContextOperation<ConfigurationDbContext> dbContextOperation,
    IOptions<ModuleConfigurationEfCoreOption> options)
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    private enum SchemaState
    {
        Missing,
        RequiresUpgrade,
        Ready
    }

    internal Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        return EnsureReadyAsync(options.Value.AutoManageSchema, cancellationToken);
    }

    internal Task UpgradeAsync(CancellationToken cancellationToken)
    {
        return EnsureReadyAsync(allowSchemaChanges: true, cancellationToken);
    }

    private async Task EnsureReadyAsync(
        bool allowSchemaChanges,
        CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await dbContextOperation.ExecuteAsync(
                (dbContext, token) => EnsureReadyAsync(dbContext, allowSchemaChanges, token),
                cancellationToken);
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static async Task EnsureReadyAsync(
        ConfigurationDbContext dbContext,
        bool allowSchemaChanges,
        CancellationToken cancellationToken)
    {
        var creator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
        {
            if (!allowSchemaChanges)
            {
                ThrowSchemaUpgradeRequired("The Monica.Configuration database does not exist.");
            }

            await creator.CreateAsync(cancellationToken);
            await creator.CreateTablesAsync(cancellationToken);
            await EnsureConfigurationSchemaMarkerAsync(dbContext, cancellationToken);
            return;
        }

        var schemaState = await GetSchemaStateAsync(dbContext, cancellationToken);
        if (schemaState == SchemaState.Missing)
        {
            if (!allowSchemaChanges)
            {
                ThrowSchemaUpgradeRequired("The Monica.Configuration database tables are missing.");
            }

            await creator.CreateTablesAsync(cancellationToken);
            await EnsureConfigurationSchemaMarkerAsync(dbContext, cancellationToken);
            return;
        }

        if (schemaState != SchemaState.RequiresUpgrade)
        {
            return;
        }

        if (!allowSchemaChanges)
        {
            ThrowSchemaUpgradeRequired("The Monica.Configuration database schema requires an upgrade.");
        }

        await EnsureAdditiveSchemaAsync(dbContext, cancellationToken);
        await EnsureConfigurationSchemaMarkerAsync(dbContext, cancellationToken);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowSchemaUpgradeRequired(string problem)
    {
        throw new ConfigurationMetadataStoreReadException(
            ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
            $"{problem} Resolve {nameof(DatabaseConfigurationStore)} and call {nameof(DatabaseConfigurationStore.UpgradeSchemaAsync)} from an "
            + "explicit deployment or startup migration step before reading or publishing metadata; automatic "
            + "schema creation is disabled for this host.");
    }

    private static async Task<SchemaState> GetSchemaStateAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        int? markerVersion = null;
        try
        {
            markerVersion = await dbContext.ConfigurationSchemaMarkers
                .AsNoTracking()
                .Where(candidate => candidate.MarkerKey == ConfigurationSchemaMarkerEntity.CurrentMarkerKey)
                .Select(candidate => (int?)candidate.SchemaVersion)
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex)
                                   || ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            // Older schemas may not have a marker yet; inspect the core tables before deciding how to upgrade.
        }

        if (markerVersion > ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            throw new ConfigurationMetadataStoreReadException(
                ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                $"The Monica.Configuration database schema is version {markerVersion}, but this runtime supports version {ConfigurationSchemaMarkerEntity.CurrentSchemaVersion}. "
                + "Deploy a newer Monica.Configuration runtime instead of letting an older binary migrate or rewrite the schema.");
        }

        try
        {
            // Do not materialize newly required CLR properties until a partially applied upgrade has normalized them.
            _ = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Select(static definition => definition.DefinitionKey)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex))
        {
            return SchemaState.Missing;
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            return SchemaState.RequiresUpgrade;
        }

        if (markerVersion != ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            return SchemaState.RequiresUpgrade;
        }

        try
        {
            _ = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Select(definition => new
                {
                    definition.DefinitionIdentity,
                    definition.SchemaJson,
                    definition.FromProject,
                    definition.Category,
                    definition.Description,
                    definition.PublishRevision
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationValueHistories
                .AsNoTracking()
                .Select(history => new
                {
                    history.HistoryId,
                    history.DefinitionIdentity,
                    history.SchemaHash
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationEffectiveValues
                .AsNoTracking()
                .Select(value => new
                {
                    value.DefinitionKey,
                    value.DefinitionIdentity
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationDefinitionPublishHistories
                .AsNoTracking()
                .Select(history => new
                {
                    history.HistoryId,
                    history.DefinitionIdentity,
                    history.DefinitionKey,
                    history.Description,
                    history.PublishedTime
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationUnifiedVersions
                .AsNoTracking()
                .Select(version => new
                {
                    version.Version,
                    version.DefinitionCount,
                    version.CreatedTime
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationUnifiedVersionDocuments
                .AsNoTracking()
                .Select(document => new
                {
                    document.Version,
                    document.DefinitionIdentity,
                    document.DefinitionKey,
                    document.SchemaHash
                })
                .FirstOrDefaultAsync(cancellationToken);
            return SchemaState.Ready;
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex)
                                   || ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            return SchemaState.RequiresUpgrade;
        }
    }

    private static async Task EnsureAdditiveSchemaAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureConfigurationSchemaMarkerTableAsync(dbContext, cancellationToken);
        await EnsureHistorySchemaHashColumnAsync(dbContext, cancellationToken);
        await EnsureUnifiedVersionTablesAsync(dbContext, cancellationToken);
        await ConfigurationDefinitionIdentitySchemaMigrator.EnsureSchemaAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureHistorySchemaHashColumnAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await dbContext.ConfigurationValueHistories
                .AsNoTracking()
                .Select(static history => history.SchemaHash)
                .FirstOrDefaultAsync(cancellationToken);
            return;
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            // Legacy rows remain null and are intentionally treated as unverified by rollback and history UI.
        }

        var providerName = dbContext.Database.ProviderName;
        var tableSql = ConfigurationDatabaseSql.FormatTableName(
            providerName,
            null,
            "ConfigurationValueHistories");
        var columnSql = ConfigurationDatabaseSql.QuoteIdentifier(
            providerName,
            nameof(ConfigurationValueHistoryEntity.SchemaHash));
        var columnType = providerName switch
        {
            var name when ConfigurationDatabaseSql.IsSqlServer(name) => "nvarchar(max)",
            var name when ConfigurationDatabaseSql.IsMySql(name) => "longtext",
            _ => "text"
        };
        var alterSql = $"ALTER TABLE {tableSql} ADD {columnSql} {columnType} NULL";
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsDuplicateColumn(ex))
        {
            // Another replica may have completed the additive upgrade after this replica's probe.
            _ = await dbContext.ConfigurationValueHistories
                .AsNoTracking()
                .Select(static history => history.SchemaHash)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    private static async Task EnsureConfigurationSchemaMarkerAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var marker = await dbContext.ConfigurationSchemaMarkers
            .FirstOrDefaultAsync(
                candidate => candidate.MarkerKey == ConfigurationSchemaMarkerEntity.CurrentMarkerKey,
                cancellationToken);
        if (marker is null)
        {
            dbContext.ConfigurationSchemaMarkers.Add(new ConfigurationSchemaMarkerEntity());
        }
        else if (marker.SchemaVersion > ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            throw new ConfigurationMetadataStoreReadException(
                ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
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
        var providerName = dbContext.Database.ProviderName;
        var tableSql = ConfigurationDatabaseSql.FormatTableName(
            providerName,
            null,
            "ConfigurationSchemaMarkers");
        var sql = ConfigurationDatabaseSql.BuildCreateTableIfMissing(
            providerName,
            "ConfigurationSchemaMarkers",
            tableSql,
            ConfigurationDatabaseSql.BuildSchemaMarkerColumns(providerName));
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureUnifiedVersionTablesAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        var versionsTableSql = ConfigurationDatabaseSql.FormatTableName(
            providerName,
            null,
            "ConfigurationUnifiedVersions");
        var documentsTableSql = ConfigurationDatabaseSql.FormatTableName(
            providerName,
            null,
            "ConfigurationUnifiedVersionDocuments");

        await dbContext.Database.ExecuteSqlRawAsync(
            ConfigurationDatabaseSql.BuildCreateTableIfMissing(
                providerName,
                "ConfigurationUnifiedVersions",
                versionsTableSql,
                ConfigurationDatabaseSql.BuildUnifiedVersionsColumns(providerName)),
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            ConfigurationDatabaseSql.BuildCreateTableIfMissing(
                providerName,
                "ConfigurationUnifiedVersionDocuments",
                documentsTableSql,
                ConfigurationDatabaseSql.BuildUnifiedVersionDocumentsColumns(providerName)),
            cancellationToken);
    }
}
