namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Captures the effective declaration and policy values required to execute and present a job after it is enqueued.
/// </summary>
/// <remarks>
/// The snapshot prevents a later declaration or policy edit from changing the semantics of work that has already
/// entered the durable queue. Identity is owner-scoped: the scheduler scope, owner, and job key together address one
/// definition.
/// </remarks>
public sealed record JobExecutionTemplate
{
    /// <summary>
    /// Gets the scheduler scope that isolates this execution.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the stable key of the application or deployment unit that owns and can execute the job.
    /// </summary>
    public required string OwnerKey { get; init; }

    /// <summary>
    /// Gets the owner-scoped logical job key used for history and the concurrency gate.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the effective user-facing job name captured with the execution.
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
    /// Gets the owner-scoped job capacity. Claims use it as the maximum concurrent lease count; recurring
    /// materialization uses it as the maximum queued-and-running occurrence count before recording a tick as skipped.
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
        JobSchedulerIdentity.ValidateStandard(SchedulerScopeKey, nameof(SchedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(OwnerKey, nameof(OwnerKey));
        JobSchedulerIdentity.ValidateJobKey(JobKey, nameof(JobKey));
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
