using System.Collections.Concurrent;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Models;

namespace Monica.Core.HostedService.Services.Support;

/// <summary>
/// Stores registered Monica hosted service instances and exposes read-only runtime queries.
/// </summary>
internal sealed class HostedServiceRegistry : IMoHostedServiceRegistry, IHostedServiceRegistryWriter
{
    private readonly ConcurrentDictionary<Type, IMoHostedService> _services = new();

    /// <inheritdoc />
    public bool Register(IMoHostedService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return _services.TryAdd(service.GetType(), service);
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetAllServices()
    {
        return _services.Values.Select(s => s.RuntimeInfo).ToList();
    }

    /// <inheritdoc />
    public HostedServiceRuntimeInfo? GetService<TService>() where TService : IMoHostedService
    {
        return GetService(typeof(TService));
    }

    /// <inheritdoc />
    public HostedServiceRuntimeInfo? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service.RuntimeInfo : null;
    }

    /// <inheritdoc />
    public HostedServiceRuntimeInfo? GetServiceByName(string serviceName)
    {
        return _services.Values
            .Select(s => s.RuntimeInfo)
            .FirstOrDefault(info => info.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByState(HostedServiceState state)
    {
        return _services.Values
            .Select(s => s.RuntimeInfo)
            .Where(info => info.CurrentState == state)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetUnhealthyServices()
    {
        return _services.Values
            .Select(s => s.RuntimeInfo)
            .Where(info => !info.IsHealthy)
            .ToList();
    }
}
