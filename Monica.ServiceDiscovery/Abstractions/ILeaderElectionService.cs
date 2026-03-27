using Monica.ServiceDiscovery.Events;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Abstractions;

/// <summary>
/// Leader election service interface
/// </summary>
public interface ILeaderElectionService
{
    /// <summary>
    /// Current Leader status
    /// </summary>
    LeaderStatus CurrentStatus { get; }

    /// <summary>
    /// Whether it is Leader
    /// </summary>
    bool IsLeader { get; }

    /// <summary>
    /// Time to become Leader (null if not Leader)
    /// </summary>
    DateTime? LeaderBecomeTime { get; }

    /// <summary>
    /// Current ETag (for renewal verification)
    /// </summary>
    string? CurrentETag { get; }

    /// <summary>
    /// Leader gets events
    /// </summary>
    event EventHandler<LeaderGainedEvent>? OnLeaderGained;

    /// <summary>
    /// Leader loss event
    /// </summary>
    event EventHandler<LeaderLostEvent>? OnLeaderLost;

    /// <summary>
    /// Set as Leader
    /// </summary>
    void SetAsLeader(DateTime becomeTime, string eTag);

    /// <summary>
    /// Update ETag
    /// </summary>
    void UpdateETag(string newETag);

    /// <summary>
    /// Trigger Leader loss
    /// </summary>
    void TriggerLeaderLost(LeaderLostReason reason);

    /// <summary>
    /// Reset Leader state
    /// </summary>
    void ResetLeaderState();
}
