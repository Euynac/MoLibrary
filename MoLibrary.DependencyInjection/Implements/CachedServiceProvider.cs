using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.DependencyInjection.Implements;

/// <summary>
/// A service provider wrapper that caches resolved services for improved performance.
/// Services are cached per scope lifetime.
/// </summary>
public class CachedServiceProvider : ICachedServiceProvider
{
    public IServiceProvider NoCachedProvider { get; }
    protected ConcurrentDictionary<ServiceIdentifier, Lazy<object?>> CachedServices { get; }

    public CachedServiceProvider(IServiceProvider serviceProvider)
    {
        NoCachedProvider = serviceProvider;
        CachedServices = new ConcurrentDictionary<ServiceIdentifier, Lazy<object?>>();
        CachedServices.TryAdd(new ServiceIdentifier(typeof(IServiceProvider)), new Lazy<object?>(() => NoCachedProvider));
    }

    public object? GetService(Type serviceType)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceType),
            _ => new Lazy<object?>(() => NoCachedProvider.GetService(serviceType))
        ).Value;
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (NoCachedProvider is not IKeyedServiceProvider keyedServiceProvider)
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
            _ => new Lazy<object?>(() => NoCachedProvider.GetRequiredKeyedService(serviceType, serviceKey))
        ).Value!;
    }
}
