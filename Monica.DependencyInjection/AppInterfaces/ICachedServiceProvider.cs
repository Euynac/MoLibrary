using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.AppInterfaces;

/// <summary>
/// Provides lazy service resolution with caching support.
/// This is the primary interface for service access in Monica.
/// </summary>
/// <remarks>
/// Services resolved through this provider are cached for the lifetime of the provider instance.
/// Since this provider is registered as Scoped, the cache is per-request/scope.
/// </remarks>
public interface ICachedServiceProvider : IKeyedServiceProvider
{ 
    /// <summary>
    /// No cached service provider.
    /// </summary>
    IServiceProvider NoCachedProvider { get; }
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
