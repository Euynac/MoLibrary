namespace Monica.JobScheduler.Models.Operations;

/// <summary>
/// Aggregates one owner's durable scheduler footprint inside one scope: definition counts by presence, active
/// queue depth, and the freshest snapshot observation time across the owner's definitions.
/// </summary>
public sealed record JobOwnerSummary
{
    /// <summary>
    /// Gets the owner identity whose snapshot published these definitions.
    /// </summary>
    public required string OwnerKey { get; init; }

    /// <summary>
    /// Gets how many of the owner's definitions its latest snapshot currently publishes.
    /// </summary>
    public required int PresentCount { get; init; }

    /// <summary>
    /// Gets how many retained definitions the owner no longer publishes. They reject new admission but keep
    /// operator policy and execution history for audit.
    /// </summary>
    public required int AbsentCount { get; init; }

    /// <summary>
    /// Gets the owner's currently queued execution count.
    /// </summary>
    public required int QueuedCount { get; init; }

    /// <summary>
    /// Gets the owner's currently running execution count.
    /// </summary>
    public required int RunningCount { get; init; }

    /// <summary>
    /// Gets the most recent <see cref="Definitions.JobDefinition.LastObservedAtUtc"/> across the owner's
    /// definitions; freshness of this value is the owner's liveness signal.
    /// </summary>
    public required DateTimeOffset LastObservedAtUtc { get; init; }
}
