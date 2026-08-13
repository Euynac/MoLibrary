namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Declares the exact job revisions executable by one worker process.
/// </summary>
public sealed record WorkerCapabilityRegistration
{
    /// <summary>
    /// Gets the scheduler scope from which work may be claimed.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the stable owner whose jobs the worker executes.
    /// </summary>
    public required string OwnerKey { get; init; }

    /// <summary>
    /// Gets the worker's immutable executable revision.
    /// </summary>
    public required string WorkerRevisionId { get; init; }

    /// <summary>
    /// Gets the concrete worker process identifier.
    /// </summary>
    public required string WorkerInstanceId { get; init; }

    /// <summary>
    /// Gets the exact job revisions present in this worker process. An empty collection is valid for a zero-job worker.
    /// </summary>
    public IReadOnlyCollection<string> JobRevisionIds { get; init; } = [];

    internal void Validate()
    {
        JobSchedulerIdentity.ValidateStandard(SchedulerScopeKey, nameof(SchedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(OwnerKey, nameof(OwnerKey));
        JobSchedulerIdentity.ValidateStandard(WorkerRevisionId, nameof(WorkerRevisionId));
        JobSchedulerIdentity.ValidateStandard(WorkerInstanceId, nameof(WorkerInstanceId));
        ArgumentNullException.ThrowIfNull(JobRevisionIds);

        foreach (var jobRevisionId in JobRevisionIds)
        {
            JobSchedulerIdentity.ValidateHash(jobRevisionId, nameof(JobRevisionIds));
        }

        if (JobRevisionIds.Count != JobRevisionIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException("Worker capability contains duplicate job revision identities.", nameof(JobRevisionIds));
        }
    }
}

/// <summary>
/// Contains the fencing values that identify one worker capability lease.
/// </summary>
public sealed record WorkerCapabilityLeaseKey
{
    /// <summary>
    /// Gets the scheduler scope containing the lease.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the concrete worker process identifier.
    /// </summary>
    public required string WorkerInstanceId { get; init; }

    /// <summary>
    /// Gets the opaque token that fences previous registrations of the same worker identifier.
    /// </summary>
    public required string LeaseToken { get; init; }

    internal void Validate()
    {
        JobSchedulerIdentity.ValidateStandard(SchedulerScopeKey, nameof(SchedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(WorkerInstanceId, nameof(WorkerInstanceId));
        JobSchedulerIdentity.ValidateStandard(LeaseToken, nameof(LeaseToken));
    }
}

/// <summary>
/// Represents a time-bounded worker capability registration.
/// </summary>
public sealed record WorkerCapabilityLease
{
    /// <summary>
    /// Gets the advertised capability.
    /// </summary>
    public required WorkerCapabilityRegistration Capability { get; init; }

    /// <summary>
    /// Gets the opaque lease key used to renew, release, and claim work.
    /// </summary>
    public required WorkerCapabilityLeaseKey LeaseKey { get; init; }

    /// <summary>
    /// Gets when the capability lease expires in UTC.
    /// </summary>
    public DateTimeOffset LeaseExpiresAtUtc { get; init; }
}

/// <summary>
/// Describes one bounded atomic claim operation for a worker capability lease.
/// </summary>
public sealed record JobClaimRequest
{
    /// <summary>
    /// Defines the hard upper bound for one atomic store claim.
    /// </summary>
    public const int MAX_COUNT = 256;
    private const int MIN_CANDIDATE_BUDGET = 64;
    private const int CANDIDATES_PER_REQUESTED_LEASE = 4;
    private const int MAX_CANDIDATE_BUDGET = MAX_COUNT * CANDIDATES_PER_REQUESTED_LEASE;

    /// <summary>
    /// Gets the active worker capability lease key.
    /// </summary>
    public required WorkerCapabilityLeaseKey CapabilityLeaseKey { get; init; }

    /// <summary>
    /// Gets the maximum number of executions to claim in this operation. A single claim is limited to 256 leases so
    /// every store implementation can enforce a finite candidate and query budget.
    /// </summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>
    /// Gets the initial duration of each claimed execution lease.
    /// </summary>
    public TimeSpan LeaseDuration { get; init; }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(CapabilityLeaseKey);
        CapabilityLeaseKey.Validate();

        if (MaxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxCount), MaxCount, "Claim count must be greater than zero.");
        }

        if (MaxCount > MAX_COUNT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxCount),
                MaxCount,
                $"Claim count cannot exceed {MAX_COUNT}.");
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LeaseDuration),
                LeaseDuration,
                "Execution lease duration must be greater than zero.");
        }
    }

    internal int GetCandidateBudget()
    {
        return Math.Clamp(
            MaxCount * CANDIDATES_PER_REQUESTED_LEASE,
            MIN_CANDIDATE_BUDGET,
            MAX_CANDIDATE_BUDGET);
    }
}
