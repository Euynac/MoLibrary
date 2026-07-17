using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.Exceptions;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseLock
{
    internal static async Task AcquireAsync(
        ConfigurationDbContext dbContext,
        string lockKey,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.ConfigurationStoreLocks
            .Where(candidate => candidate.LockKey == lockKey)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.LockVersion,
                    candidate => candidate.LockVersion + 1),
                cancellationToken);
        if (affectedRows != 1)
        {
            throw new ConfigurationStoreSchemaException(
                $"The Monica.Configuration database is missing the required '{lockKey}' store lock. "
                + "Apply the host-owned EF Core migrations for ConfigurationDbContext before using the configuration store.");
        }
    }
}
