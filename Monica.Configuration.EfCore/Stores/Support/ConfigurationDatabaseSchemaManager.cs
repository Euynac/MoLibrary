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
        Incompatible,
        Ready
    }

    internal Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        return EnsureReadyAsync(options.Value.AutoManageSchema, cancellationToken);
    }

    internal Task InitializeAsync(CancellationToken cancellationToken)
    {
        return EnsureReadyAsync(allowSchemaCreation: true, cancellationToken);
    }

    private async Task EnsureReadyAsync(
        bool allowSchemaCreation,
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
                (dbContext, token) => EnsureReadyAsync(dbContext, allowSchemaCreation, token),
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
        bool allowSchemaCreation,
        CancellationToken cancellationToken)
    {
        var creator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
        {
            if (!allowSchemaCreation)
            {
                ThrowSchemaInitializationRequired("The Monica.Configuration database does not exist.");
            }

            await creator.CreateAsync(cancellationToken);
            await creator.CreateTablesAsync(cancellationToken);
            await EnsureConfigurationSchemaMarkerAsync(dbContext, cancellationToken);
            return;
        }

        var schemaState = await GetSchemaStateAsync(dbContext, cancellationToken);
        if (schemaState == SchemaState.Missing)
        {
            if (!allowSchemaCreation)
            {
                ThrowSchemaInitializationRequired("The Monica.Configuration database tables are missing.");
            }

            await creator.CreateTablesAsync(cancellationToken);
            await EnsureConfigurationSchemaMarkerAsync(dbContext, cancellationToken);
            return;
        }

        if (schemaState != SchemaState.Incompatible)
        {
            return;
        }

        throw new ConfigurationMetadataStoreReadException(
            ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
            "The Monica.Configuration schema predates the current definition-revision and publisher-state model. "
            + "Schema version 8 requires a fresh database; retain the existing database only as an external archive.");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowSchemaInitializationRequired(string problem)
    {
        throw new ConfigurationMetadataStoreReadException(
            ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
            $"{problem} Resolve {nameof(DatabaseConfigurationStore)} and call {nameof(DatabaseConfigurationStore.InitializeSchemaAsync)} from an "
            + "explicit deployment or startup initialization step before reading or publishing metadata; automatic "
            + "schema creation is disabled for this host.");
    }

    private static async Task<SchemaState> GetSchemaStateAsync(
        ConfigurationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        int? markerVersion = null;
        var markerTableExists = true;
        try
        {
            markerVersion = await dbContext.ConfigurationSchemaMarkers
                .AsNoTracking()
                .Where(candidate => candidate.MarkerKey == ConfigurationSchemaMarkerEntity.CurrentMarkerKey)
                .Select(candidate => (int?)candidate.SchemaVersion)
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex))
        {
            markerTableExists = false;
            // Earlier schemas may not have a marker; inspect the core tables before deciding compatibility.
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            return SchemaState.Incompatible;
        }

        if (markerVersion > ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            throw new ConfigurationMetadataStoreReadException(
                ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                $"The Monica.Configuration database schema is version {markerVersion}, but this runtime supports version {ConfigurationSchemaMarkerEntity.CurrentSchemaVersion}. "
                + "Deploy a newer Monica.Configuration runtime instead of letting an older binary migrate or rewrite the schema.");
        }

        if (markerTableExists && markerVersion != ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            return SchemaState.Incompatible;
        }

        try
        {
            // Probe only the legacy key until compatibility is established; newer required properties may not exist.
            _ = await dbContext.ConfigurationDefinitions
                .AsNoTracking()
                .Select(static definition => definition.DefinitionKey)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingTable(ex))
        {
            return markerTableExists ? SchemaState.Incompatible : SchemaState.Missing;
        }
        catch (Exception ex) when (ConfigurationDatabaseExceptionClassifier.IsMissingColumn(ex))
        {
            return SchemaState.Incompatible;
        }

        if (markerVersion != ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            return SchemaState.Incompatible;
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
                    definition.DefinitionRevision
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
                    history.DefinitionRevision,
                    history.PublishedTime
                })
                .FirstOrDefaultAsync(cancellationToken);
            _ = await dbContext.ConfigurationDefinitionPublisherStates
                .AsNoTracking()
                .Select(state => new
                {
                    state.DefinitionIdentity,
                    state.PublisherIdentity,
                    state.PublisherKey,
                    state.ObservationKind,
                    state.ReloadBehavior
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
            return SchemaState.Incompatible;
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
        else if (marker.SchemaVersion != ConfigurationSchemaMarkerEntity.CurrentSchemaVersion)
        {
            throw new ConfigurationMetadataStoreReadException(
                ConfigurationMetadataStoreIssueKind.IncompatibleStoreSchema,
                $"The Monica.Configuration database schema is version {marker.SchemaVersion}, but this runtime supports version {ConfigurationSchemaMarkerEntity.CurrentSchemaVersion}.");
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

}
