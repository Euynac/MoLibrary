namespace Monica.ServiceDiscovery.Events;

/// <summary>
/// Service Orphaned Incident
/// </summary>
public class ServiceIsolatedEvent
{
    /// <summary>
    /// Service name
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Instance ID
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Orphaned time detected
    /// </summary>
    public required DateTime IsolationDetectedTime { get; init; }

    /// <summary>
    /// Last successful heartbeat time
    /// </summary>
    public DateTime? LastSuccessfulHeartbeat { get; init; }
}
