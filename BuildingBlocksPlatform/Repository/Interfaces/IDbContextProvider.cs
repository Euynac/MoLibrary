using Microsoft.EntityFrameworkCore;

namespace BuildingBlocksPlatform.Repository.Interfaces;

public interface IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Asynchronously gets the database context.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the database context.</returns>
    Task<TDbContext> GetDbContextAsync();
}


public class DefaultDbContextProvider<TDbContext>(TDbContext context) : IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    public Task<TDbContext> GetDbContextAsync()
    {
        return Task.FromResult(context);
    }
}