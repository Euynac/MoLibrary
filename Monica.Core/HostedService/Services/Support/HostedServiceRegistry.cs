using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Models;

namespace Monica.Core.HostedService.Services.Support;

/// <summary>
/// Stores registered Monica hosted service instances and exposes read-only runtime queries.
/// </summary>
internal sealed class HostedServiceRegistry : IMoHostedServiceRegistry, IHostedServiceRegistryWriter
{
    private IMoHostedService[] _services = [];

    /// <inheritdoc />
    public void Publish(IReadOnlyList<IMoHostedService> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var snapshot = services.ToArray();
        var distinctInstances = new HashSet<IMoHostedService>(ReferenceEqualityComparer.Instance);
        foreach (var service in snapshot)
        {
            if (!distinctInstances.Add(service))
            {
                throw new InvalidOperationException(
                    $"Hosted service instance '{service.ServiceName}' is registered more than once as IHostedService.");
            }
        }

        var duplicateId = snapshot
            .GroupBy(static service => service.RuntimeInfo.InstanceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new InvalidOperationException(
                $"Hosted service instance ID '{duplicateId.Key}' is not unique within the current host.");
        }

        Volatile.Write(ref _services, snapshot);
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetAllServices()
    {
        return GetSnapshot().Select(static service => service.RuntimeInfo).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServices<TService>() where TService : IMoHostedService
    {
        return GetServices(typeof(TService));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServices(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return GetSnapshot()
            .Where(service => service.GetType() == serviceType)
            .Select(static service => service.RuntimeInfo)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByName(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return GetSnapshot()
            .Select(static service => service.RuntimeInfo)
            .Where(info => info.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByKey(string? serviceKey)
    {
        return GetSnapshot()
            .Select(static service => service.RuntimeInfo)
            .Where(info => string.Equals(info.ServiceKey, serviceKey, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc />
    public HostedServiceRuntimeInfo? GetServiceByInstanceId(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return GetSnapshot()
            .Select(static service => service.RuntimeInfo)
            .SingleOrDefault(info => string.Equals(info.InstanceId, instanceId, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByState(HostedServiceState state)
    {
        return GetSnapshot()
            .Select(static service => service.RuntimeInfo)
            .Where(info => info.CurrentState == state)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceRuntimeInfo> GetUnhealthyServices()
    {
        return GetSnapshot()
            .Select(static service => service.RuntimeInfo)
            .Where(info => !info.IsHealthy)
            .ToArray();
    }

    private IMoHostedService[] GetSnapshot() => Volatile.Read(ref _services);
}
