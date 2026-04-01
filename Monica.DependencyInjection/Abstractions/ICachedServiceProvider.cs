using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.Abstractions;

/// <summary>
/// Provides service resolution with per-instance caching.
/// </summary>
/// <remarks>
/// Resolution results are cached by service type and key for the lifetime of the provider instance.
/// In the default registration, this provider is scoped, so the cache is scoped as well.
/// </remarks>
public interface ICachedServiceProvider : IKeyedServiceProvider
{
    /// <summary>
    /// Gets the underlying service provider without the per-instance cache layer.
    /// </summary>
    IServiceProvider UnderlyingProvider { get; }
}
