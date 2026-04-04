using Microsoft.EntityFrameworkCore;

namespace Monica.Repository.Persistence.Abstractions;

public interface IDbContextDatabaseManager<TDbContext> where TDbContext : DbContext
{
    /// <summary>
    /// Check if database is created.
    /// </summary>
    /// <returns></returns>
    Task<bool> CheckDatabaseAsync();

    /// <summary>
    /// Initialize or update the database.
    /// </summary>
    /// <returns></returns>
    Task InitOrUpdateDatabaseAsync();

    /// <summary>
    /// Check if database update is needed.
    /// </summary>
    /// <returns></returns>
    Task<bool> CheckIfDbUpdateNeededAsync();
}

public class DbContextDatabaseManager<TDbContext>(TDbContext context)
    : IDbContextDatabaseManager<TDbContext>
    where TDbContext : DbContext
{

    public async Task<bool> CheckDatabaseAsync()
    {
        return await context.Database.CanConnectAsync();
    }

    public async Task InitOrUpdateDatabaseAsync()
    {
        await context.Database.MigrateAsync();
    }

    public async Task<bool> CheckIfDbUpdateNeededAsync()
    {
        return (await context.Database.GetPendingMigrationsAsync()).Any();
    }
}