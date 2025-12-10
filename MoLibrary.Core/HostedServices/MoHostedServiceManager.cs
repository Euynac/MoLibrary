using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices;

/// <summary>
/// Implementation of IMoHostedServiceManager that tracks all registered MoHostedServices
/// </summary>
public class MoHostedServiceManager : IMoHostedServiceManager
{
    private readonly ConcurrentDictionary<Type, HostedServiceObservableInfo> _serviceRegistry = new();
    private readonly ConcurrentDictionary<Type, ExceptionPool> _exceptionPools = new();

    /// <summary>
    /// Registers a service with the manager (called by registration extensions)
    /// </summary>
    /// <param name="serviceType">The service type</param>
    /// <param name="info">Observable information for the service</param>
    /// <param name="pool">Exception pool for the service (optional)</param>
    internal void RegisterService(Type serviceType, HostedServiceObservableInfo info, ExceptionPool? pool)
    {
        _serviceRegistry[serviceType] = info;
        if (pool != null)
        {
            _exceptionPools[serviceType] = pool;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetAllServices()
    {
        return _serviceRegistry.Values.ToList();
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService<TService>() where TService : IHostedService
    {
        return GetService(typeof(TService));
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService(Type serviceType)
    {
        return _serviceRegistry.TryGetValue(serviceType, out var info) ? info : null;
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetServiceByName(string serviceName)
    {
        return _serviceRegistry.Values.FirstOrDefault(s =>
            s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetServicesByState(HostedServiceState state)
    {
        return _serviceRegistry.Values.Where(s => s.CurrentState == state).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetUnhealthyServices()
    {
        return _serviceRegistry.Values.Where(s => !s.IsHealthy).ToList();
    }

    /// <inheritdoc />
    public ExceptionPool? GetServiceExceptionPool(Type serviceType)
    {
        return _exceptionPools.TryGetValue(serviceType, out var pool) ? pool : null;
    }
}
