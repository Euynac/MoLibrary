using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Identifies the exact code revision that owns and can execute a job instance.
/// </summary>
/// <remarks>
/// Every execution captures this identity when it is enqueued. Workers must advertise the same owner and worker
/// revision and explicitly include the job revision in their capability lease before they can claim the instance.
/// </remarks>
public sealed record JobRevisionIdentity
{
    /// <summary>
    /// Gets the scheduler scope that isolates releases and executions.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the immutable release identity from which this job revision originated.
    /// </summary>
    public required string CatalogReleaseId { get; init; }

    /// <summary>
    /// Gets the monotonic activation epoch assigned when the release became active.
    /// </summary>
    public long ActivationEpoch { get; init; }

    /// <summary>
    /// Gets the stable key of the application or deployment unit that owns the job.
    /// </summary>
    public required string OwnerKey { get; init; }

    /// <summary>
    /// Gets the executable worker revision required to run this job revision.
    /// </summary>
    public required string WorkerRevisionId { get; init; }

    /// <summary>
    /// Gets the content identity of the immutable job declaration.
    /// </summary>
    public required string JobRevisionId { get; init; }

    /// <summary>
    /// Gets the scope-wide logical job key used for history and the concurrency gate. The gate intentionally excludes
    /// owner and revision identity so a running superseded revision still fences its replacement after an owner move.
    /// </summary>
    public required string JobKey { get; init; }

    internal void Validate()
    {
        JobSchedulerIdentity.ValidateStandard(SchedulerScopeKey, nameof(SchedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(CatalogReleaseId, nameof(CatalogReleaseId));
        JobSchedulerIdentity.ValidateStandard(OwnerKey, nameof(OwnerKey));
        JobSchedulerIdentity.ValidateStandard(WorkerRevisionId, nameof(WorkerRevisionId));
        JobSchedulerIdentity.ValidateHash(JobRevisionId, nameof(JobRevisionId));
        JobSchedulerIdentity.ValidateJobKey(JobKey, nameof(JobKey));

        if (ActivationEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ActivationEpoch),
                ActivationEpoch,
                "An activation epoch must be greater than zero.");
        }
    }
}

/// <summary>
/// Captures the immutable declaration values required to execute and present a job after it is enqueued.
/// </summary>
/// <remarks>
/// The snapshot prevents a later catalog activation or policy edit from changing the semantics of work that has
/// already entered the durable queue.
/// </remarks>
public sealed record JobExecutionTemplate
{
    /// <summary>
    /// Gets the exact job revision identity.
    /// </summary>
    public required JobRevisionIdentity Revision { get; init; }

    /// <summary>
    /// Gets the immutable user-facing job name captured with the execution.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets whether the job is recurring or explicitly triggered.
    /// </summary>
    public JobType JobType { get; init; }

    /// <summary>
    /// Gets the serialized argument type key for a triggered job, or <see langword="null"/> for a recurring job.
    /// </summary>
    public string? JobArgsKey { get; init; }

    /// <summary>
    /// Gets the scope-wide logical job capacity. Claims use it as the maximum concurrent lease count; recurring
    /// materialization uses it as the maximum queued-and-running occurrence count before recording a tick as skipped.
    /// Both checks include superseded owners and revisions.
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Gets the number of retries allowed after the first failed execution attempt.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the maximum duration of one execution attempt.
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; init; } = TimeSpan.FromHours(1);

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Revision);
        Revision.Validate();
        JobSchedulerIdentity.ValidateStandard(JobName, nameof(JobName));

        if (!Enum.IsDefined(JobType))
        {
            throw new ArgumentOutOfRangeException(nameof(JobType), JobType, "The job type is not supported.");
        }

        if (JobType == JobType.Triggered)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(JobArgsKey);
        }

        if (MaxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrency),
                MaxConcurrency,
                "Maximum concurrency must be greater than zero.");
        }

        if (RetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryCount),
                RetryCount,
                "Retry count cannot be negative.");
        }

        if (MaxExecutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxExecutionTimeout),
                MaxExecutionTimeout,
                "Execution timeout must be greater than zero.");
        }
    }
}
