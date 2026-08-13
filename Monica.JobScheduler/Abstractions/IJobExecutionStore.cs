using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Defines the atomic persistence boundary for durable, revision-aware job execution.
/// </summary>
/// <remarks>
/// Implementations own queue availability, distributed concurrency gates, worker capabilities, execution leases,
/// fencing, retries, cancellation, recurring cursor materialization, and history. Callers must not emulate these
/// operations with read-modify-write sequences. A concurrency gate is identified by scheduler scope and logical job
/// key, so it continues to fence overlapping work when a catalog activation moves that job to another owner or code
/// revision.
/// </remarks>
public interface IJobExecutionStore
{
    /// <summary>
    /// Durably enqueues an execution. Repeating an identical request is idempotent. When availability is omitted, the
    /// store captures its authoritative current time only for the initial insert and ignores availability on retries.
    /// </summary>
    Task<JobExecutionInstance> EnqueueAsync(
        JobEnqueueRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one execution by scope and identifier, including its complete retained audit history.
    /// </summary>
    Task<JobExecutionInstance?> GetExecutionAsync(
        string schedulerScopeKey,
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries executions through a bounded page. Returned execution snapshots omit audit history; callers that need
    /// history must load a selected execution with <see cref="GetExecutionAsync"/>.
    /// </summary>
    Task<QueryResult<JobExecutionInstance>> QueryExecutionsAsync(
        JobExecutionQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets execution counts grouped by state within an optional enqueue-time range.
    /// </summary>
    Task<IReadOnlyDictionary<JobExecutionState, int>> GetExecutionStateStatisticsAsync(
        string schedulerScopeKey,
        DateTimeOffset? startTimeUtc = null,
        DateTimeOffset? endTimeUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets at most one newest execution summary for every distinct requested logical job key. Durable providers keep
    /// each per-key read bounded, and returned snapshots omit audit history.
    /// </summary>
    Task<IReadOnlyDictionary<string, JobExecutionInstance?>> GetLatestExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the globally oldest terminal executions eligible under per-job count and age retention.
    /// </summary>
    /// <remarks>
    /// The returned collection never exceeds <paramref name="maxDeletions"/>. A positive bound is required so durable
    /// providers can evaluate arbitrarily large histories without materializing the entire terminal set.
    /// </remarks>
    Task<IReadOnlyList<string>> GetExecutionCleanupCandidatesAsync(
        string schedulerScopeKey,
        IReadOnlyDictionary<string, JobHistoryRetentionPolicy> retentionPolicies,
        int maxRetainedOrphanedExecutions,
        int maxDeletions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes terminal executions. Running or queued work is never deleted.
    /// </summary>
    Task<int> DeleteExecutionsAsync(
        string schedulerScopeKey,
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new capability lease, fences any previous registration for the same worker identifier, and prunes
    /// expired capability rows from the same scheduler scope using the store's authoritative time.
    /// </summary>
    Task<WorkerCapabilityLease> RegisterWorkerCapabilityAsync(
        WorkerCapabilityRegistration registration,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews an existing worker capability lease, returning <see langword="null"/> when it has been fenced or expired.
    /// </summary>
    Task<WorkerCapabilityLease?> RenewWorkerCapabilityAsync(
        WorkerCapabilityLeaseKey leaseKey,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a current worker capability lease.
    /// </summary>
    Task<bool> ReleaseWorkerCapabilityAsync(
        WorkerCapabilityLeaseKey leaseKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets currently active worker capabilities for operational convergence reporting.
    /// </summary>
    Task<IReadOnlyList<WorkerCapabilityLease>> GetActiveWorkerCapabilitiesAsync(
        string schedulerScopeKey,
        string? ownerKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims due compatible work while enforcing the distributed concurrency gate for each
    /// <c>(SchedulerScopeKey, JobKey)</c> logical job identity across owners, releases, and worker revisions.
    /// </summary>
    Task<IReadOnlyList<JobExecutionLease>> ClaimAsync(
        JobClaimRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a current execution lease and reports persisted cancellation requests.
    /// </summary>
    Task<JobLeaseRenewalResult> RenewLeaseAsync(
        JobLeaseKey leaseKey,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically completes a fenced attempt, applying retry policy and releasing its distributed concurrency gate.
    /// </summary>
    Task<JobAttemptCompletionResult> CompleteAttemptAsync(
        JobAttemptCompletion completion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues a fenced execution after user code has fully exited during graceful host shutdown.
    /// </summary>
    Task<JobAttemptCompletionResult> ReleaseLeaseAsync(
        JobLeaseKey leaseKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels queued work immediately or persists a cooperative cancellation request for running work.
    /// </summary>
    Task<JobCancellationResult> RequestCancellationAsync(
        string schedulerScopeKey,
        string instanceId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a worker log entry only while the supplied fenced execution lease remains current. Implementations
    /// enforce the configured total per-execution history count and history-message length boundaries atomically.
    /// </summary>
    Task<JobLeaseMutationStatus> AppendExecutionLogAsync(
        JobExecutionLogEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues expired running work, or cancels it when a cancellation request was already persisted.
    /// </summary>
    Task<IReadOnlyList<JobExecutionInstance>> RecoverExpiredLeasesAsync(
        ExpiredLeaseRecoveryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes one recurring cursor with a newly observed catalog change epoch. The store calculates a new
    /// active cursor from the durable activation boundary and a resumed cursor from its authoritative current time.
    /// </summary>
    Task<RecurringScheduleSynchronizationResult> SynchronizeRecurringScheduleAsync(
        RecurringScheduleSynchronization synchronization,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets due recurring cursors belonging to the current activation in deterministic occurrence-time order. A
    /// caller may repeatedly query after advancing cursors to share a global materialization budget fairly across the
    /// overdue set.
    /// </summary>
    Task<IReadOnlyList<RecurringScheduleCursor>> GetDueRecurringSchedulesAsync(
        string schedulerScopeKey,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically materializes one recurring occurrence and compare-and-swap advances its cursor.
    /// Repeated attempts after a successful cursor advance return a stale-cursor result.
    /// </summary>
    Task<RecurringMaterializationResult> TryMaterializeRecurringOccurrenceAsync(
        RecurringOccurrenceMaterialization materialization,
        CancellationToken cancellationToken = default);
}
