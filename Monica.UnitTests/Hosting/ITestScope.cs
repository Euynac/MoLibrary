using Monica.Repository.Persistence.Services;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Represents one isolated sociable test scope.
/// </summary>
public interface ITestScope : IAsyncDisposable
{
    /// <summary>
    /// Gets the service provider for this test scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the cancellation token that should be passed to async operations in this scope.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Resolves a service from this scope.
    /// </summary>
    T Resolve<T>()
        where T : notnull;

    /// <summary>
    /// Resolves a service from this scope.
    /// </summary>
    object Resolve(Type type);

    /// <summary>
    /// Seeds the active DbContext for this scope.
    /// </summary>
    Task SeedAsync(params object[] entities);

    /// <summary>
    /// Resolves the active DbContext for this scope.
    /// </summary>
    Task<TDbContext> GetDbContextAsync<TDbContext>()
        where TDbContext : RepositoryDbContext<TDbContext>;
}
