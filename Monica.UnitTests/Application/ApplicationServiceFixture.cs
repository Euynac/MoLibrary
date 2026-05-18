using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Abstractions;
using Monica.WebApi.Abstractions;

namespace Monica.UnitTests.Application;

/// <summary>
/// Creates a DI-backed application-service instance for fast-path handler tests.
/// </summary>
/// <typeparam name="THandler">The application service under test.</typeparam>
public sealed class ApplicationServiceFixture<THandler> : IDisposable, IAsyncDisposable
    where THandler : ApplicationService
{
    private readonly ServiceProvider _rootProvider;
    private readonly AsyncServiceScope _scope;
    private bool _disposed;

    internal ApplicationServiceFixture(
        ServiceProvider rootProvider,
        AsyncServiceScope scope,
        THandler service,
        ICachedServiceProvider cachedServiceProvider)
    {
        _rootProvider = rootProvider;
        _scope = scope;
        Service = service;
        Services = scope.ServiceProvider;
        CachedServiceProvider = cachedServiceProvider;
    }

    /// <summary>
    /// Gets the application service instance created for the fixture.
    /// </summary>
    public THandler Service { get; }

    /// <summary>
    /// Gets the scoped service provider used to build the fixture.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the cached service provider assigned to the handler instance.
    /// </summary>
    public ICachedServiceProvider CachedServiceProvider { get; }

    /// <summary>
    /// Creates a builder for the fixture.
    /// </summary>
    public static ApplicationServiceFixtureBuilder<THandler> Builder() => new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();
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
        await _rootProvider.DisposeAsync();
    }
}
