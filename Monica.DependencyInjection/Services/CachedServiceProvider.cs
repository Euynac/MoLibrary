using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Models.Internal;

namespace Monica.DependencyInjection.Services;

/// <summary>
/// Wraps an underlying service provider and caches resolution results for the lifetime of the wrapper instance.
/// </summary>
public class CachedServiceProvider : ICachedServiceProvider
{
    public IServiceProvider UnderlyingProvider { get; }
    protected ConcurrentDictionary<ServiceIdentifier, Lazy<object?>> CachedServices { get; } = new();

    public CachedServiceProvider(IServiceProvider underlyingProvider)
    {
        UnderlyingProvider = underlyingProvider;
        CachedServices.TryAdd(new ServiceIdentifier(typeof(IServiceProvider)), new Lazy<object?>(() => UnderlyingProvider));
    }

    public object? GetService(Type serviceType)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceType),
            _ => new Lazy<object?>(() => UnderlyingProvider.GetService(serviceType))
        ).Value;
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (UnderlyingProvider is not IKeyedServiceProvider keyedServiceProvider)
        {
            throw new InvalidOperationException("This container does not support keyed services.");
        }

        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceKey, serviceType),
            _ => new Lazy<object?>(() => keyedServiceProvider.GetKeyedService(serviceType, serviceKey))
        ).Value;
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceKey, serviceType),
            _ => new Lazy<object?>(() => UnderlyingProvider.GetRequiredKeyedService(serviceType, serviceKey))
        ).Value!;
    }
}
