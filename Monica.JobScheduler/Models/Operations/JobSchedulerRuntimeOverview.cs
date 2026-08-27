namespace Monica.JobScheduler.Models.Operations;

/// <summary>
/// One host's runtime view of the scheduler: its effective configuration, storage identity, local scheduling
/// planes, and the durable per-owner footprint of the configured scope.
/// </summary>
/// <remarks>
/// The configuration and local-plane sections describe the host that served the request; the owner section is
/// read from the shared durable store and therefore spans every service publishing into the scope.
/// </remarks>
public sealed record JobSchedulerRuntimeOverview
{
    /// <summary>
    /// Gets the effective scheduler configuration of the serving host.
    /// </summary>
    public required JobSchedulerRuntimeConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the durable storage identity backing the scheduler store.
    /// </summary>
    public required JobSchedulerStoreInfo Store { get; init; }

    /// <summary>
    /// Gets the serving host's in-process scheduling and worker plane readiness.
    /// </summary>
    public required JobSchedulerLocalPlaneSnapshot LocalPlane { get; init; }

    /// <summary>
    /// Gets every owner's durable footprint in the configured scope, ordered by owner key.
    /// </summary>
    public required IReadOnlyList<JobOwnerSummary> Owners { get; init; }

    /// <summary>
    /// Gets the staleness boundary for owner liveness: an owner whose <c>LastObservedAtUtc</c> is older than
    /// this threshold is considered offline. The value is derived from <c>SnapshotSyncInterval</c> so the
    /// contract travels with the data instead of being reinvented by each consumer.
    /// </summary>
    public required TimeSpan OwnerOnlineThreshold { get; init; }

    /// <summary>
    /// Gets when the runtime snapshot was captured.
    /// </summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }
}

/// <summary>
/// Captures the scalar scheduler configuration that shapes runtime behavior, snapshotted from module options.
/// Non-scalar settings such as serializer options are intentionally excluded.
/// </summary>
public sealed record JobSchedulerRuntimeConfiguration
{
    /// <summary>Gets the scheduler scope every durable operation is keyed by.</summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>Gets the serving host's owner identity.</summary>
    public required string OwnerKey { get; init; }

    /// <summary>Gets the serving host's worker instance identity.</summary>
    public required string WorkerInstanceId { get; init; }

    /// <summary>Gets whether automatic recurring materialization is suppressed for manual-only debugging.</summary>
    public required bool RecurringJobDebugMode { get; init; }

    /// <summary>Gets the identifier of the time zone cron schedules resolve in.</summary>
    public required string CronTimeZoneId { get; init; }

    /// <summary>Gets the scheduling plane's poll cadence.</summary>
    public required TimeSpan SchedulingPollInterval { get; init; }

    /// <summary>Gets the worker plane's claim poll cadence.</summary>
    public required TimeSpan WorkerPollInterval { get; init; }

    /// <summary>Gets how often a host republishes its complete local definition snapshot.</summary>
    public required TimeSpan SnapshotSyncInterval { get; init; }

    /// <summary>Gets the duration of each fenced execution lease.</summary>
    public required TimeSpan ExecutionLeaseDuration { get; init; }

    /// <summary>Gets how often a running attempt renews its execution lease.</summary>
    public required TimeSpan ExecutionLeaseRenewInterval { get; init; }

    /// <summary>Gets the delay applied before a failed attempt becomes retryable.</summary>
    public required TimeSpan ExecutionRetryDelay { get; init; }

    /// <summary>Gets the cooperative-exit budget after timeout, durable cancellation, or lease loss.</summary>
    public required TimeSpan ExecutionCancellationGracePeriod { get; init; }

    /// <summary>Gets the total drain budget for graceful worker shutdown.</summary>
    public required TimeSpan WorkerShutdownGracePeriod { get; init; }

    /// <summary>Gets the worker's maximum concurrent execution count.</summary>
    public required int MaxWorkerExecutionThreads { get; init; }

    /// <summary>Gets the maximum execution count claimed in one store batch.</summary>
    public required int MaxClaimBatchSize { get; init; }

    /// <summary>Gets the recurring occurrence budget shared per scheduling cycle.</summary>
    public required int MaxRecurringMaterializationsPerCycle { get; init; }

    /// <summary>Gets the expired-lease recovery budget per scheduling cycle.</summary>
    public required int MaxExpiredLeaseRecoveriesPerCycle { get; init; }

    /// <summary>Gets whether historical execution cleanup is enabled.</summary>
    public required bool EnableHistoryCleanup { get; init; }

    /// <summary>Gets how often history cleanup runs.</summary>
    public required TimeSpan HistoryCleanupInterval { get; init; }

    /// <summary>Gets the maximum history rows deleted per cleanup cycle.</summary>
    public required int MaxHistoryDeletionsPerCycle { get; init; }

    /// <summary>Gets the maximum history entries retained per execution.</summary>
    public required int MaxExecutionHistoryEntriesPerExecution { get; init; }

    /// <summary>Gets the maximum persisted length of one history message.</summary>
    public required int MaxExecutionHistoryMessageLength { get; init; }

    /// <summary>Gets the maximum orphaned terminal executions retained before cleanup.</summary>
    public required int MaxRetainedOrphanedExecutions { get; init; }
}

/// <summary>
/// Identifies the durable storage backing the scheduler for operational display.
/// </summary>
public sealed record JobSchedulerStoreInfo
{
    /// <summary>Gets the storage family name, for example "InMemory" or "EF Core".</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the optional provider name, for example the Entity Framework provider identifier.
    /// </summary>
    public string? Provider { get; init; }
}

/// <summary>
/// Snapshots the serving host's in-process scheduling and worker plane readiness. These facts are host-local;
/// other replicas of the same owner are not visible here.
/// </summary>
public sealed record JobSchedulerLocalPlaneSnapshot
{
    /// <summary>Gets whether the scheduling plane's last cycle succeeded.</summary>
    public required bool SchedulingReady { get; init; }

    /// <summary>Gets the scheduling plane's latest readiness message, when one was recorded.</summary>
    public required string? SchedulingMessage { get; init; }

    /// <summary>Gets whether the worker plane's last cycle succeeded.</summary>
    public required bool WorkerReady { get; init; }

    /// <summary>Gets the worker plane's latest readiness message, when one was recorded.</summary>
    public required string? WorkerMessage { get; init; }

    /// <summary>Gets the local worker's current in-flight execution count.</summary>
    public required int InFlightExecutions { get; init; }
}
