using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Models.Operations;

/// <summary>
/// Describes the runtime state of an active job's recurring schedule.
/// </summary>
public enum JobRecurringScheduleStatus
{
    /// <summary>
    /// The active declaration is triggered rather than recurring.
    /// </summary>
    NotRecurring,

    /// <summary>
    /// The recurring declaration is active, but its current activation has not created a durable cursor yet.
    /// </summary>
    AwaitingSynchronization,

    /// <summary>
    /// The durable cursor has a future occurrence that can be materialized.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Materialization is suspended by effective operator policy or by the durable cursor.
    /// </summary>
    Suspended,

    /// <summary>
    /// The durable cursor has no future occurrence under the declared schedule.
    /// </summary>
    Exhausted
}

/// <summary>
/// Projects the bounded operational state associated with one active job definition.
/// </summary>
/// <remarks>
/// The active definition remains the source of truth for immutable declaration and operator policy. Runtime fields
/// are read-only projections: the latest execution follows the logical job key across catalog revisions, while
/// compatible workers must advertise the active definition's exact owner, worker revision, and job revision.
/// </remarks>
public sealed record JobOperationalSummary
{
    /// <summary>
    /// Gets the current immutable declaration and independent operator policy.
    /// </summary>
    public required ActiveJobDefinition Definition { get; init; }

    /// <summary>
    /// Gets the recurring runtime state for the current active revision.
    /// </summary>
    public JobRecurringScheduleStatus RecurringScheduleStatus { get; init; }

    /// <summary>
    /// Gets the next UTC occurrence for a scheduled recurring job. Suspended, exhausted, unsynchronized, and
    /// triggered jobs return <see langword="null"/>.
    /// </summary>
    public DateTimeOffset? NextOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets the newest execution for the logical job key, or <see langword="null"/> when it has never run. The
    /// bounded snapshot omits retained audit history; use the execution detail query when history is required.
    /// </summary>
    public JobExecutionInstance? LatestExecution { get; init; }

    /// <summary>
    /// Gets the number of durable queued executions for the logical job key across catalog revisions.
    /// </summary>
    public int QueuedExecutionCount { get; init; }

    /// <summary>
    /// Gets the number of durable running executions for the logical job key across catalog revisions.
    /// </summary>
    public int RunningExecutionCount { get; init; }

    /// <summary>
    /// Gets the total number of non-terminal queued and running executions for the logical job key.
    /// </summary>
    public int ActiveExecutionCount => QueuedExecutionCount + RunningExecutionCount;

    /// <summary>
    /// Gets the number of live worker capability leases that can execute the active job revision.
    /// </summary>
    public int CompatibleWorkerCount { get; init; }

    /// <summary>
    /// Gets whether at least one currently registered worker can execute the active job revision.
    /// </summary>
    public bool HasCompatibleWorker => CompatibleWorkerCount > 0;
}

/// <summary>
/// Describes one policy replacement in a bounded batch operation.
/// </summary>
/// <remarks>
/// Policy fields are replacement values, matching <see cref="JobPolicyChange"/> semantics. The expected concurrency
/// stamp fences changes based on a stale operational page.
/// </remarks>
public sealed record JobPolicyBatchUpdateItem
{
    /// <summary>
    /// Gets the active catalog owner of the logical job.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the globally unique logical job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the replacement disabled override. <see langword="null"/> restores the declaration default.
    /// </summary>
    public required bool? DisabledOverride { get; init; }

    /// <summary>
    /// Gets the replacement retained-history count, or <see langword="null"/> for no count limit.
    /// </summary>
    public required int? MaxRetainedHistoryRecords { get; init; }

    /// <summary>
    /// Gets the replacement retention age in days, or <see langword="null"/> for no age limit.
    /// </summary>
    public required int? MaxRetentionDays { get; init; }

    /// <summary>
    /// Gets the policy concurrency stamp observed by the caller.
    /// </summary>
    public required string ExpectedConcurrencyStamp { get; init; }

    internal JobPolicyChange ToChange() => new()
    {
        DisabledOverride = DisabledOverride,
        MaxRetainedHistoryRecords = MaxRetainedHistoryRecords,
        MaxRetentionDays = MaxRetentionDays,
        ExpectedConcurrencyStamp = ExpectedConcurrencyStamp
    };
}

/// <summary>
/// Requests independent policy replacements for a finite set of active jobs.
/// </summary>
public sealed record JobPolicyBatchUpdateRequest
{
    /// <summary>
    /// Defines the hard upper bound for one facade request.
    /// </summary>
    public const int MAX_ITEM_COUNT = 100;

    /// <summary>
    /// Gets the policy replacements to attempt. Items are processed independently and in request order.
    /// </summary>
    public required IReadOnlyList<JobPolicyBatchUpdateItem> Items { get; init; }
}

/// <summary>
/// Reports the outcome of one item in a partial-success policy batch.
/// </summary>
public sealed record JobPolicyBatchUpdateItemResult
{
    /// <summary>
    /// Gets the active catalog owner supplied by the request.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the logical job key supplied by the request.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the updated policy when the item succeeded.
    /// </summary>
    public JobPolicy? Policy { get; init; }

    /// <summary>
    /// Gets the failure detail when the item failed validation, lookup, or optimistic concurrency.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets whether this item completed successfully.
    /// </summary>
    public bool IsSucceeded => Policy is not null;
}

/// <summary>
/// Contains ordered per-item outcomes for a bounded policy batch.
/// </summary>
public sealed record JobPolicyBatchUpdateResult
{
    /// <summary>
    /// Gets one outcome for every requested item, preserving request order.
    /// </summary>
    public required IReadOnlyList<JobPolicyBatchUpdateItemResult> Items { get; init; }

    /// <summary>
    /// Gets the number of successful policy replacements.
    /// </summary>
    public int SucceededCount => Items.Count(static item => item.IsSucceeded);

    /// <summary>
    /// Gets the number of failed policy replacements.
    /// </summary>
    public int FailedCount => Items.Count - SucceededCount;
}

/// <summary>
/// Provides one operational snapshot of catalog convergence, worker capability, and durable queue state.
/// </summary>
public sealed record JobSchedulerOverview
{
    /// <summary>
    /// Gets desired-release publication and transition status.
    /// </summary>
    public required JobCatalogPublicationStatus CatalogPublication { get; init; }

    /// <summary>
    /// Gets the currently active immutable catalog, or <see langword="null"/> before first activation.
    /// </summary>
    public JobCatalogSnapshot? ActiveCatalog { get; init; }

    /// <summary>
    /// Gets queue counts grouped by durable execution state.
    /// </summary>
    public required IReadOnlyDictionary<JobExecutionState, int> ExecutionStateCounts { get; init; }

    /// <summary>
    /// Gets desired owner publication and live worker convergence.
    /// </summary>
    public required IReadOnlyList<JobOwnerConvergence> Owners { get; init; }

    /// <summary>
    /// Gets a bounded newest-first execution activity sample.
    /// </summary>
    public required IReadOnlyList<JobExecutionInstance> RecentExecutions { get; init; }

    /// <summary>
    /// Gets when the operational snapshot finished loading.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>
    /// Gets whether the desired catalog is active and every desired owner has both published and registered a worker.
    /// </summary>
    public bool IsConverged =>
        CatalogPublication.Version.ActiveReleaseId is not null
        && CatalogPublication.Version.DesiredReleaseId is not null
        && string.Equals(
            CatalogPublication.Version.ActiveReleaseId,
            CatalogPublication.Version.DesiredReleaseId,
            StringComparison.Ordinal)
        && !CatalogPublication.Version.IsTransitionInProgress
        && CatalogPublication.MissingOwnerIds.Count == 0
        && Owners.All(static owner => owner.IsConverged);
}

/// <summary>
/// Projects the independent publication and runtime-capability signals for one desired catalog owner.
/// </summary>
public sealed record JobOwnerConvergence
{
    /// <summary>
    /// Gets the stable deployment-unit identity.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the exact executable revision expected by the desired release.
    /// </summary>
    public required string WorkerRevisionId { get; init; }

    /// <summary>
    /// Gets whether the immutable declaration snapshot has been published.
    /// </summary>
    public bool HasPublishedSnapshot { get; init; }

    /// <summary>
    /// Gets the number of active worker capability leases matching the desired revision.
    /// </summary>
    public int ActiveWorkerCount { get; init; }

    /// <summary>
    /// Gets the number of definitions from this exact owner revision in the active catalog.
    /// </summary>
    public int ActiveDefinitionCount { get; init; }

    /// <summary>
    /// Gets whether both the declaration and executable-capability signals are present.
    /// </summary>
    public bool IsConverged => HasPublishedSnapshot && ActiveWorkerCount > 0;
}

/// <summary>
/// Describes one operator-initiated, catalog-resolved execution admission request.
/// </summary>
public sealed record JobTriggerRequest
{
    /// <summary>
    /// Gets the active logical job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets serialized triggered-job arguments. The payload must match the declaration's argument contract.
    /// </summary>
    public string? JobArgs { get; init; }

    /// <summary>
    /// Gets the earliest claim time. When omitted, the store atomically captures its authoritative current time while
    /// creating the execution; idempotent retries do not assert a newly computed timestamp.
    /// </summary>
    public DateTimeOffset? AvailableAtUtc { get; init; }

    /// <summary>
    /// Gets an optional idempotency identifier. A generated identifier is used when omitted.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Gets an optional expected owner used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedOwnerId { get; init; }

    /// <summary>
    /// Gets an optional expected job revision used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedJobRevisionId { get; init; }
}

/// <summary>
/// Describes an operator-initiated immediate execution of an active recurring job.
/// </summary>
public sealed record JobRecurringRunNowRequest
{
    /// <summary>
    /// Gets the active logical recurring job key.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets an optional idempotency identifier. A generated identifier is used when omitted.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// Gets an optional expected owner used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedOwnerId { get; init; }

    /// <summary>
    /// Gets an optional expected job revision used to fence a caller compiled against an older catalog.
    /// </summary>
    public string? ExpectedJobRevisionId { get; init; }
}
