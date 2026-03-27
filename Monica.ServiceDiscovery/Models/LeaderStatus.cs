namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// leader status enum
/// </summary>
public enum LeaderStatus
{
    /// <summary>
    /// The current instance is the leader
    /// </summary>
    Leader,

    /// <summary>
    /// The current instance is a follower (other instances are leaders)
    /// </summary>
    Follower,

    /// <summary>
    /// Looking for leader (currently no leader)
    /// </summary>
    Looking
}

/// <summary>
/// Leader status query response
/// </summary>
public class LeaderStatusResponse
{
    /// <summary>
    /// The leader status of the current instance
    /// </summary>
    public LeaderStatus Status { get; set; }

    /// <summary>
    /// The instance ID of the current leader (if one exists)
    /// </summary>
    public string? LeaderInstanceId { get; set; }

    /// <summary>
    /// Leader registration time (if exists)
    /// </summary>
    public DateTime? LeaderRegistrationTime { get; set; }

    /// <summary>
    /// Total number of instances currently running
    /// </summary>
    public int RunningInstanceCount { get; set; }

    /// <summary>
    /// additional message
    /// </summary>
    public string? Message { get; set; }

    public override string ToString()
    {
        return $"Status: {Status}, LeaderInstanceId: {LeaderInstanceId}, LeaderRegistrationTime: {LeaderRegistrationTime}, RunningInstanceCount: {RunningInstanceCount}, Message: {Message}";
    }
}
