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
    /// Application identifier that owns the leader key.
    /// </summary>
    public string? AppId { get; set; }
}
