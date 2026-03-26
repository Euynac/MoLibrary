namespace Monica.ServiceDiscovery.Events;

/// <summary>
/// Event published when a service instance goes offline (detected via heartbeat timeout or explicit deregistration)
/// </summary>
public class ServiceInstanceOfflineEvent
{
    /// <summary>
    /// The unique identifier of the service instance that went offline
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// The project name of the offline service instance
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// The timestamp when the instance was detected as offline
    /// </summary>
    public required DateTime OfflineTime { get; init; }
}
