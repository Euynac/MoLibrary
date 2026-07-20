using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;

namespace Monica.Testing.Hosting;

/// <summary>
/// Represents one dependency-injection scope owned by a <see cref="MonicaTestApplication"/>.
/// </summary>
public sealed class MonicaTestScope : IAsyncDisposable
{
    private readonly AsyncServiceScope _scope;
    private bool _disposed;

    internal MonicaTestScope(AsyncServiceScope scope, CancellationToken cancellationToken)
    {
        _scope = scope;
        CancellationToken = cancellationToken;
        ServiceProvider = scope.ServiceProvider;
    }

    /// <summary>
    /// Gets the scoped service provider.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the cancellation token test operations in this scope should observe.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Resolves a required service from this scope.
    /// </summary>
    public T Resolve<T>()
        where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Resolves a required service from this scope.
    /// </summary>
    public object Resolve(Type type)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ServiceProvider.GetRequiredService(type);
    }

    /// <summary>
    /// Adds entities to the single repository DbContext registered in this scope and saves them.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scenario has no registered repository DbContext or has more than one candidate.
    /// </exception>
    public async Task SeedAsync(params object[] entities)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (entities.Length == 0)
        {
            return;
        }

        var registry = ServiceProvider.GetService<TestDbContextTypeRegistry>();
        if (registry is null || registry.Types.Count == 0)
        {
            throw new InvalidOperationException("No RepositoryDbContext service is registered in this test scope.");
        }

        var dbContextType = registry.Types.Count == 1
            ? registry.Types.Single()
            : throw new InvalidOperationException(
                "Multiple RepositoryDbContext services are registered. Resolve the intended DbContext and seed it explicitly.");

        var dbContext = (Microsoft.EntityFrameworkCore.DbContext)Resolve(dbContextType);
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// Resolves the repository DbContext registered for this scope.
    /// </summary>
    public async Task<TDbContext> GetDbContextAsync<TDbContext>()
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var provider = ServiceProvider.GetService<IDbContextProvider<TDbContext>>();
        return provider is null
            ? Resolve<TDbContext>()
            : await provider.GetDbContextAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _scope.DisposeAsync();
    }
}
