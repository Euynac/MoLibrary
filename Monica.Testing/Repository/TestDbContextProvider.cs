using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Testing.Repository;

/// <summary>
/// Returns the DbContext owned by a test fixture.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type.</typeparam>
public sealed class TestDbContextProvider<TDbContext>(IServiceProvider serviceProvider, TDbContext? context = null)
    : IDbContextProvider<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public Task<TDbContext> GetDbContextAsync()
    {
        return Task.FromResult(context ?? serviceProvider.GetRequiredService<TDbContext>());
    }
}
