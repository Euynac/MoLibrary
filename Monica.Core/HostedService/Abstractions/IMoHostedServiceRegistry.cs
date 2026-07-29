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
    /// Gets all hosted-service instances whose concrete type matches <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">The concrete hosted-service type.</typeparam>
    /// <returns>An ordered, read-only list of matching runtime information.</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetServices<TService>() where TService : IMoHostedService;

    /// <summary>
    /// Gets all hosted-service instances whose concrete type matches <paramref name="serviceType"/>.
    /// </summary>
    /// <param name="serviceType">The concrete hosted-service type.</param>
    /// <returns>An ordered, read-only list of matching runtime information.</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetServices(Type serviceType);

    /// <summary>
    /// Gets all hosted-service instances with the specified display name.
    /// </summary>
    /// <param name="serviceName">The case-insensitive service name.</param>
    /// <returns>An ordered, read-only list of matching runtime information.</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByName(string serviceName);

    /// <summary>
    /// Gets all hosted-service instances with the specified dependency-injection key.
    /// </summary>
    /// <param name="serviceKey">The service key, or <see langword="null"/> for default instances.</param>
    /// <returns>An ordered, read-only list of matching runtime information.</returns>
    IReadOnlyList<HostedServiceRuntimeInfo> GetServicesByKey(string? serviceKey);

    /// <summary>
    /// Gets one hosted-service instance by its exact runtime identity.
    /// </summary>
    /// <param name="instanceId">The instance identity exposed by <see cref="HostedServiceRuntimeInfo.InstanceId"/>.</param>
    /// <returns>The matching runtime information, or <see langword="null"/> when the instance is not registered.</returns>
    HostedServiceRuntimeInfo? GetServiceByInstanceId(string instanceId);

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
