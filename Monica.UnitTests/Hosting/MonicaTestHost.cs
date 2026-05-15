using Microsoft.Extensions.DependencyInjection;
using Monica.UnitTests.Modularity;

namespace Monica.UnitTests.Hosting;

/// <summary>
/// Lightweight host for module-guide tests that need service collection composition without a full xUnit fixture.
/// </summary>
public sealed class MonicaTestHost : IDisposable
{
    private ServiceProvider? _provider;
    private bool _disposed;

    internal MonicaTestHost(ModuleTestScope moduleScope, IServiceCollection services)
    {
        ModuleScope = moduleScope;
        Services = services;
    }

    /// <summary>
    /// Gets the module state scope.
    /// </summary>
    public ModuleTestScope ModuleScope { get; }

    /// <summary>
    /// Gets services configured for the test host.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Creates a builder for a module test host.
    /// </summary>
    public static MonicaTestHostBuilder Create(params System.Reflection.Assembly[] discoveryAssemblies)
    {
        return new MonicaTestHostBuilder(discoveryAssemblies);
    }

    /// <summary>
    /// Builds and caches the service provider.
    /// </summary>
    public IServiceProvider Build()
    {
        return _provider ??= Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider?.Dispose();
        ModuleScope.Dispose();
    }
}
