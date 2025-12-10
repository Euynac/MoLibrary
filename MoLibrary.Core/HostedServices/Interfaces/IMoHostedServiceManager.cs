using MoLibrary.Core.HostedServices.Models;

namespace MoLibrary.Core.HostedServices.Interfaces;

/// <summary>
/// Provides centralized management and query capabilities for all registered MoHostedServices
/// </summary>
public interface IMoHostedServiceManager
{
    /// <summary>
    /// Registers a hosted service with the manager (called internally during startup)
    /// </summary>
    /// <param name="service">The hosted service instance to register</param>
    void RegisterService(IMoHostedService service);

    /// <summary>
    /// Gets all registered hosted services
    /// </summary>
    /// <returns>A readonly list of observable information for all registered services</returns>
    IReadOnlyList<HostedServiceObservableInfo> GetAllServices();

    /// <summary>
    /// Gets observable information for a specific service by type
    /// </summary>
    /// <typeparam name="TService">The hosted service type</typeparam>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceObservableInfo? GetService<TService>() where TService : IMoHostedService;

    /// <summary>
    /// Gets observable information for a specific service by type
    /// </summary>
    /// <param name="serviceType">The hosted service type</param>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceObservableInfo? GetService(Type serviceType);

    /// <summary>
    /// Gets observable information for a service by its name
    /// </summary>
    /// <param name="serviceName">The service name</param>
    /// <returns>Observable information for the service, or null if not found</returns>
    HostedServiceObservableInfo? GetServiceByName(string serviceName);

    /// <summary>
    /// Gets all services in a specific state
    /// </summary>
    /// <param name="state">The state to filter by</param>
    /// <returns>A readonly list of services in the specified state</returns>
    IReadOnlyList<HostedServiceObservableInfo> GetServicesByState(HostedServiceState state);

    /// <summary>
    /// Gets all services that are not healthy (Faulted or Degraded)
    /// </summary>
    /// <returns>A readonly list of unhealthy services</returns>
    IReadOnlyList<HostedServiceObservableInfo> GetUnhealthyServices();
}
