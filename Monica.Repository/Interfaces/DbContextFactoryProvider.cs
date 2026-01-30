using Microsoft.EntityFrameworkCore;

namespace Monica.Repository.Interfaces;

/// <summary>
/// DbContext provider that wraps EF Core's IDbContextFactory for use in long-lived services (Singletons).
/// This provider bridges Monica's IDbContextProvider abstraction with EF Core's official factory pattern,
/// enabling thread-safe DbContext access from Singleton services while maintaining backward compatibility.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type</typeparam>
public class DbContextFactoryProvider<TDbContext>(IDbContextFactory<TDbContext> factory)
    : IDbContextProvider<TDbContext>, IAsyncDisposable
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly AsyncLocal<TDbContext?> _currentContext = new();

    /// <summary>
    /// Gets or creates a DbContext for the current async flow.
    /// Returns the same instance within the same async call chain (AsyncLocal optimization).
    /// </summary>
    public Task<TDbContext> GetDbContextAsync()
    {
        // Reuse context in same async flow (optimization to avoid creating multiple contexts)
        if (_currentContext.Value != null)
        {
            return Task.FromResult(_currentContext.Value);
        }

        // Create new via factory (uses DbContext pooling if enabled)
        var context = _factory.CreateDbContext();
        _currentContext.Value = context;
        return Task.FromResult(context);
    }

    /// <summary>
    /// Disposes the DbContext for the current async flow.
    /// Should be called when the operation is complete to return the context to the pool.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var context = _currentContext.Value;
        if (context != null)
        {
            await context.DisposeAsync();
            _currentContext.Value = null;
        }

        GC.SuppressFinalize(this);
    }
}
