namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// Leader status
/// </summary>
public class LeaderState
{
    /// <summary>
    /// Leader instance ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// Time to become a Leader
    /// </summary>
    public DateTime BecomeLeaderTime { get; set; }

    /// <summary>
    /// Service name
    /// </summary>
    public string? ServiceName { get; set; }
}
