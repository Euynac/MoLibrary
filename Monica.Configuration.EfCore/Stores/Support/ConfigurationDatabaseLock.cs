using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseLock
{
    internal static async Task EnsureMarkerExistsAsync(
        ConfigurationDbContext dbContext,
        string markerKey,
        CancellationToken cancellationToken)
    {
        var markerExists = await dbContext.ConfigurationSchemaMarkers
            .AnyAsync(candidate => candidate.MarkerKey == markerKey, cancellationToken);
        if (markerExists)
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
            if (await dbContext.ConfigurationSchemaMarkers
                    .AnyAsync(candidate => candidate.MarkerKey == markerKey, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    internal static async Task AcquireAsync(
        ConfigurationDbContext dbContext,
        string markerKey,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            ConfigurationDatabaseSql.BuildStoreLockUpdate(dbContext.Database.ProviderName),
            [markerKey],
            cancellationToken);
    }
}
