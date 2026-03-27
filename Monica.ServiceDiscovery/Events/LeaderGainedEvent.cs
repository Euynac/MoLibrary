namespace Monica.ServiceDiscovery.Events;

/// <summary>
/// Leader gets events
/// </summary>
public class LeaderGainedEvent
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
    /// Time to become a Leader
    /// </summary>
    public required DateTime BecomeLeaderTime { get; init; }
}
