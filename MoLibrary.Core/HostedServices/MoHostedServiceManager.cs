using System.Collections.Concurrent;
using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.Core.HostedServices.Interfaces;
using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices;

/// <summary>
/// Implementation of IMoHostedServiceManager that tracks all registered MoHostedServices
/// </summary>
public class MoHostedServiceManager : IMoHostedServiceManager
{
    private readonly ConcurrentDictionary<Type, IMoHostedService> _services = new();

    /// <inheritdoc />
    public void RegisterService(IMoHostedService service)
    {
        _services[service.GetType()] = service;
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetAllServices()
    {
        return _services.Values.Select(s => s.ObservableInfo).ToList();
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService<TService>() where TService : IMoHostedService
    {
        return GetService(typeof(TService));
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service.ObservableInfo : null;
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetServiceByName(string serviceName)
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .FirstOrDefault(info => info.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetServicesByState(HostedServiceState state)
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .Where(info => info.CurrentState == state)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetUnhealthyServices()
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .Where(info => !info.IsHealthy)
            .ToList();
    }

    /// <inheritdoc />
    public ExceptionPool? GetServiceExceptionPool(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service.ExceptionPool : null;
    }
}
