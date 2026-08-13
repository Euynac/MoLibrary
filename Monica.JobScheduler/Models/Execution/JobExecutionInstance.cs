using Microsoft.Extensions.Logging;

namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Represents the durable lifecycle state of a queued job execution.
/// </summary>
public enum JobExecutionState
{
    /// <summary>
    /// The execution is durable and becomes claimable when its availability time is reached.
    /// </summary>
    Queued,

    /// <summary>
    /// A compatible worker owns a time-bounded fenced lease for the execution.
    /// </summary>
    Running,

    /// <summary>
    /// The execution completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The execution exhausted its configured retries.
    /// </summary>
    Failed,

    /// <summary>
    /// The execution was cancelled before or during execution.
    /// </summary>
    Cancelled
}

/// <summary>
/// Classifies an entry in a job execution's bounded retained history.
/// </summary>
public enum JobExecutionHistoryKind
{
    /// <summary>
    /// The entry records a durable state transition.
    /// </summary>
    StateTransition,

    /// <summary>
    /// The entry was emitted by executing job code.
    /// </summary>
    ExecutionLog,

    /// <summary>
    /// The entry records an operator cancellation request.
    /// </summary>
    Cancellation
}

/// <summary>
/// Describes one immutable retained audit entry for a job execution.
/// </summary>
public sealed record JobExecutionHistoryEntry
{
    /// <summary>
    /// Gets when the entry was recorded in UTC.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Gets the category of the history entry.
    /// </summary>
    public JobExecutionHistoryKind Kind { get; init; }

    /// <summary>
    /// Gets the state before the entry, when the entry represents a transition.
    /// </summary>
    public JobExecutionState? PreviousState { get; init; }

    /// <summary>
    /// Gets the state after the entry, when the entry represents a transition.
    /// </summary>
    public JobExecutionState? NewState { get; init; }

    /// <summary>
    /// Gets the diagnostic severity of the entry.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Gets the developer-facing history message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the worker instance associated with this entry, when applicable.
    /// </summary>
    public string? WorkerInstanceId { get; init; }
}

/// <summary>
/// Provides an immutable snapshot of one durable job execution.
/// </summary>
public sealed record JobExecutionInstance
{
    /// <summary>
    /// Gets the unique caller- or scheduler-assigned execution identifier.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the immutable declaration snapshot captured when the execution was enqueued.
    /// </summary>
    public required JobExecutionTemplate Template { get; init; }

    /// <summary>
    /// Gets the serialized triggered-job arguments, or <see langword="null"/> for a recurring job.
    /// </summary>
    public string? JobArgs { get; init; }

    /// <summary>
    /// Gets the earliest UTC time at which a worker may claim the execution.
    /// </summary>
    public DateTimeOffset AvailableAtUtc { get; init; }

    /// <summary>
    /// Gets the current durable state.
    /// </summary>
    public JobExecutionState State { get; init; }

    /// <summary>
    /// Gets when the execution was enqueued in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets when the most recent execution attempt began in UTC.
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>
    /// Gets when the execution reached a terminal state in UTC.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets the number of times a worker has successfully claimed this execution.
    /// </summary>
    public int ExecutionAttempt { get; init; }

    /// <summary>
    /// Gets the number of failed attempts that have consumed retry policy.
    /// </summary>
    public int RetryAttempt { get; init; }

    /// <summary>
    /// Gets the number of expired worker leases recovered by the store.
    /// </summary>
    public int LeaseLossCount { get; init; }

    /// <summary>
    /// Gets the current worker instance while the execution is running.
    /// </summary>
    public string? RunningWorkerInstanceId { get; init; }

    /// <summary>
    /// Gets when the current execution lease expires in UTC.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets when cancellation was requested, if the request is still relevant to this execution.
    /// </summary>
    public DateTimeOffset? CancellationRequestedAtUtc { get; init; }

    /// <summary>
    /// Gets the immutable audit history for this snapshot. Detail reads populate the complete retained history;
    /// queue pages and mutation results return an empty collection to keep operational paths bounded.
    /// </summary>
    public IReadOnlyList<JobExecutionHistoryEntry> History { get; init; } = [];

    /// <summary>
    /// Gets whether this execution has reached a terminal state.
    /// </summary>
    public bool IsTerminal => State is JobExecutionState.Succeeded or JobExecutionState.Failed or JobExecutionState.Cancelled;
}
