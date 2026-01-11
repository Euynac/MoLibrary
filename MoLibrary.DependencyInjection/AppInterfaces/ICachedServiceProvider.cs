using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.AppInterfaces;

/// <summary>
/// Provides lazy service resolution with caching support.
/// This is the primary interface for service access in MoLibrary.
/// </summary>
/// <remarks>
/// Services resolved through this provider are cached for the lifetime of the provider instance.
/// Since this provider is registered as Scoped, the cache is per-request/scope.
/// </remarks>
public interface ICachedServiceProvider : IKeyedServiceProvider
{
}

/// <summary>
/// Interface for classes that require lazy service provider injection via property.
/// Implement this interface to enable automatic property injection by the DI system.
/// </summary>
public interface ICachedServiceProviderInjector
{
    /// <summary>
    /// Gets or sets the lazy service provider.
    /// This property will be automatically injected by the DI system when using dynamic proxies.
    /// </summary>
    ICachedServiceProvider ServiceProvider { get; set; }
}
