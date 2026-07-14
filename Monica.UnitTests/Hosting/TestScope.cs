using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Default implementation of <see cref="ITestScope"/> for compatibility-layer fixtures.
/// </summary>
public sealed class TestScope : ITestScope
{
    private readonly AsyncServiceScope _scope;
    private readonly IAsyncDisposable? _providerAsyncDisposable;
    private readonly IDisposable? _providerDisposable;
    private bool _disposed;

    public TestScope(
        AsyncServiceScope scope,
        IAsyncDisposable? providerAsyncDisposable,
        IDisposable? providerDisposable,
        CancellationToken cancellationToken)
    {
        _scope = scope;
        _providerAsyncDisposable = providerAsyncDisposable;
        _providerDisposable = providerDisposable;
        CancellationToken = cancellationToken;
        ServiceProvider = scope.ServiceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public CancellationToken CancellationToken { get; }

    public T Resolve<T>()
        where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    public object Resolve(Type type)
    {
        return ServiceProvider.GetRequiredService(type);
    }

    public async Task SeedAsync(params object[] entities)
    {
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
                "Multiple RepositoryDbContext services are registered. Use GetDbContextAsync<TDbContext>() and seed explicitly.");

        var dbContext = (Microsoft.EntityFrameworkCore.DbContext)Resolve(dbContextType);
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    public async Task<TDbContext> GetDbContextAsync<TDbContext>()
        where TDbContext : RepositoryDbContext<TDbContext>
    {
        var provider = ServiceProvider.GetService<IDbContextProvider<TDbContext>>();
        if (provider is not null)
        {
            return await provider.GetDbContextAsync();
        }

        return Resolve<TDbContext>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _scope.DisposeAsync();
        if (_providerAsyncDisposable is not null)
        {
            await _providerAsyncDisposable.DisposeAsync();
        }
        else
        {
            _providerDisposable?.Dispose();
        }
    }
}
