namespace Monica.JobScheduler.UI.UIJobScheduler.Abstractions;

/// <summary>
/// One observed worker instance from an optional infrastructure-integration provider, typically a service
/// discovery registry with heartbeats. Correlation with scheduler owners relies on both sides resolving the
/// same application-level project name.
/// </summary>
public sealed record JobSchedulerWorkerInstanceView
{
    /// <summary>Gets the registry's stable service identifier, for example the AppId.</summary>
    public required string AppId { get; init; }

    /// <summary>Gets the display name of the registered service.</summary>
    public required string AppName { get; init; }

    /// <summary>Gets the project name the instance reports; scheduler owners with this key own its jobs.</summary>
    public required string ProjectName { get; init; }

    /// <summary>Gets the registry's unique instance identifier for one running process.</summary>
    public required string InstanceId { get; init; }

    /// <summary>Gets whether the registry elected this instance as its service leader.</summary>
    public required bool IsLeader { get; init; }

    /// <summary>Gets when the instance first registered.</summary>
    public required DateTimeOffset RegisteredAtUtc { get; init; }

    /// <summary>Gets the instance's most recent heartbeat time.</summary>
    public required DateTimeOffset LastHeartbeatUtc { get; init; }

    /// <summary>Gets the client-side liveness classification derived from heartbeat freshness.</summary>
    public required JobSchedulerWorkerInstanceStatus Status { get; init; }

    /// <summary>Gets the network addresses the instance listens on, when reported. May be empty.</summary>
    public IReadOnlyList<string> ListeningAddresses { get; init; } = [];
}

/// <summary>
/// Classifies worker-instance liveness from heartbeat freshness. Offline is a client-side derivation; the
/// registry itself removes expired instances from its durable view.
/// </summary>
public enum JobSchedulerWorkerInstanceStatus
{
    /// <summary>The heartbeat is fresher than 1.5 heartbeat periods.</summary>
    Online,

    /// <summary>The heartbeat missed the healthy boundary but has not crossed the registration TTL.</summary>
    Unhealthy,

    /// <summary>The heartbeat crossed the registration TTL; the instance is presumed offline.</summary>
    Offline
}

/// <summary>
/// Optionally enriches the JobScheduler runtime view with worker-instance heartbeats from an infrastructure
/// provider. The runtime page resolves this from the service provider and degrades to a setup hint when absent,
/// so hosts that do not adopt the integration keep the rest of the runtime tab.
/// </summary>
public interface IJobSchedulerWorkerInsightProvider
{
    /// <summary>
    /// Loads every worker instance the backing registry currently observes, flattened across services and
    /// ordered by project name then instance identifier.
    /// </summary>
    /// <param name="cancellationToken">Cancels the registry read.</param>
    /// <returns>The registry's instance list; empty when nothing has registered yet.</returns>
    Task<IReadOnlyList<JobSchedulerWorkerInstanceView>> GetWorkerInstancesAsync(
        CancellationToken cancellationToken = default);
}
