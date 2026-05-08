namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// Describes the current registry runtime state exposed by the API.
/// </summary>
public sealed class RegistryServiceStatusResponse
{
    /// <summary>
    /// Gets the current service instance snapshot.
    /// </summary>
    public required RegistryCurrentInstanceStatus CurrentInstance { get; init; }

    /// <summary>
    /// Gets every registered instance currently visible in the registry.
    /// </summary>
    public List<InstanceState> RegisteredInstances { get; init; } = [];

    /// <summary>
    /// Gets the current cluster leader information when available.
    /// </summary>
    public RegistryLeaderInfo? LeaderInfo { get; init; }
}

/// <summary>
/// Describes the current service instance from the registry perspective.
/// </summary>
public sealed class RegistryCurrentInstanceStatus
{
    /// <summary>
    /// Gets the current instance information.
    /// </summary>
    public required InstanceState InstanceInfo { get; init; }

    /// <summary>
    /// Gets whether the current instance is the cluster leader.
    /// </summary>
    public bool IsLeader { get; init; }

    /// <summary>
    /// Gets the current leader election status name.
    /// </summary>
    public required string LeaderStatus { get; init; }

    /// <summary>
    /// Gets when the current instance became leader, when applicable.
    /// </summary>
    public DateTime? LeaderBecomeTime { get; init; }
}

/// <summary>
/// Describes the active cluster leader recorded by the registry.
/// </summary>
public sealed class RegistryLeaderInfo
{
    /// <summary>
    /// Gets the leader instance identifier.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets when the leader assumed leadership.
    /// </summary>
    public DateTime BecomeLeaderTime { get; init; }

    /// <summary>
    /// Gets the leader application identifier.
    /// </summary>
    public string? AppId { get; init; }
}
