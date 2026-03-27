using Monica.Core.HostedService.Models;

namespace Monica.Core.HostedService.Abstractions;

/// <summary>
/// Provides centralized query capabilities for registered Monica hosted services.
/// </summary>
public interface IMoHostedServiceRegistry
{
    /// <summary>
    /// Gets all registered hosted services.
    /// </summary>
    /// <returns>A read-only list of runtime information for all registered services.</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetAllServices();

    /// <summary>
    /// Gets observable information for a specific service by type
    /// </summary>
    /// <typeparam name="TService">The hosted service type</typeparam>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceRuntimeInfo? GetService<TService>() where TService : IMoHostedService;

    /// <summary>
    /// Gets observable information for a specific service by type
    /// </summary>
    /// <param name="serviceType">The hosted service type</param>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceRuntimeInfo? GetService(Type serviceType);

    /// <summary>
    /// Gets observable information for a service by its name
    /// </summary>
    /// <param name="serviceName">The service name</param>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceRuntimeInfo? GetServiceByName(string serviceName);

    /// <summary>
    /// Gets all services in a specific state
    /// </summary>
    /// <param name="state">The state to filter by</param>
    /// <returns>A readonly list of services in the specified state</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByState(HostedServiceState state);

    /// <summary>
    /// Gets all services that are not healthy (Faulted or Degraded)
    /// </summary>
    /// <returns>A readonly list of unhealthy services</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetUnhealthyServices();
}
