namespace Monica.JobScheduler.Models.Catalog;

using Monica.JobScheduler.Models.Execution;

/// <summary>
/// Identifies one owner and the exact worker revision that may execute its jobs in a catalog release.
/// </summary>
public sealed record JobCatalogOwnerManifest(string OwnerId, string WorkerRevisionId);

/// <summary>
/// Describes the complete owner membership of one immutable scheduler release. An empty membership is a valid
/// control-plane release that decommissions every owner and job.
/// </summary>
public sealed record JobCatalogReleaseManifest(
    string SchedulerScopeKey,
    string ReleaseId,
    IReadOnlyList<JobCatalogOwnerManifest> Owners)
{
    /// <summary>
    /// Gets the deterministic hash of the normalized owner manifest.
    /// </summary>
    public string ContentHash => JobCatalogHash.ComputeManifest(this);
}

/// <summary>
/// Stages one immutable release under a deployment orchestrator's monotonic generation.
/// </summary>
public sealed record JobCatalogReleaseStage(
    JobCatalogReleaseManifest Manifest,
    long DeploymentGeneration);

/// <summary>
/// Contains the immutable, code-owned contract of one job.
/// </summary>
public sealed record JobDeclaration
{
    /// <summary>
    /// Maximum persisted length of a display name.
    /// </summary>
    public const int JOB_NAME_MAX_LENGTH = 128;

    /// <summary>
    /// Maximum persisted length of an optional description.
    /// </summary>
    public const int DESCRIPTION_MAX_LENGTH = 4_000;

    /// <summary>
    /// Maximum persisted length of a Cron expression.
    /// </summary>
    public const int CRON_EXPRESSION_MAX_LENGTH = 512;

    /// <summary>
    /// Gets the scope-wide logical identity. One release cannot declare the same key under multiple owners, and a
    /// concurrency gate continues across owner and revision changes for this key.
    /// </summary>
    public required string JobKey { get; init; }
    public string? JobArgsKey { get; init; }
    public required string JobName { get; init; }
    public string? Description { get; init; }
    public JobType JobType { get; init; }
    /// <summary>
    /// Gets the scope-wide logical job capacity across owners and revisions. It limits active leases and determines
    /// when overlapping recurring occurrences are recorded as skipped instead of queued.
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;
    public int RetryCount { get; init; }
    public TimeSpan MaxExecutionTimeout { get; init; } = TimeSpan.FromHours(1);
    public bool IsDisabledByDefault { get; init; }
    public string? CronExpression { get; init; }
    public string? TimeZoneId { get; init; }
    public DateTimeOffset? StartTimeUtc { get; init; }
    public DateTimeOffset? EndTimeUtc { get; init; }

    internal JobDeclaration NormalizeAndValidate()
    {
        var normalized = this with
        {
            JobName = JobName?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            CronExpression = CronExpression is null
                ? null
                : JobScheduleOverride.NormalizeCronExpression(CronExpression),
            TimeZoneId = TimeZoneId?.Trim(),
            StartTimeUtc = StartTimeUtc?.ToUniversalTime(),
            EndTimeUtc = EndTimeUtc?.ToUniversalTime()
        };
        normalized.Validate();
        return normalized;
    }

    internal void Validate()
    {
        JobSchedulerIdentity.ValidateJobKey(JobKey, nameof(JobKey));
        if (string.IsNullOrWhiteSpace(JobName) || JobName.Length > JOB_NAME_MAX_LENGTH)
        {
            throw new ArgumentException(
                $"Job name must contain between 1 and {JOB_NAME_MAX_LENGTH} characters.",
                nameof(JobName));
        }

        if (Description?.Length > DESCRIPTION_MAX_LENGTH)
        {
            throw new ArgumentException(
                $"Job description cannot exceed {DESCRIPTION_MAX_LENGTH} characters.",
                nameof(Description));
        }

        if (!Enum.IsDefined(JobType))
        {
            throw new ArgumentOutOfRangeException(nameof(JobType), JobType, "Unsupported job type.");
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
            throw new ArgumentOutOfRangeException(nameof(RetryCount), RetryCount, "Retry count cannot be negative.");
        }

        if (MaxExecutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxExecutionTimeout),
                MaxExecutionTimeout,
                "Execution timeout must be greater than zero.");
        }

        if (JobType == JobType.Triggered)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(JobArgsKey);
            JobSchedulerIdentity.ValidateStandard(JobArgsKey, nameof(JobArgsKey));
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(CronExpression);
        if (CronExpression.Length > CRON_EXPRESSION_MAX_LENGTH)
        {
            throw new ArgumentException(
                $"Cron expression cannot exceed {CRON_EXPRESSION_MAX_LENGTH} characters.",
                nameof(CronExpression));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(TimeZoneId);
        new RecurringScheduleDefinition
        {
            CronExpression = CronExpression,
            TimeZoneId = TimeZoneId,
            StartTimeUtc = StartTimeUtc,
            EndTimeUtc = EndTimeUtc
        }.Validate();
    }
}

/// <summary>
/// Publishes the complete declaration set for one expected owner and worker revision.
/// </summary>
public sealed record JobOwnerCatalogSnapshot(
    string SchedulerScopeKey,
    string ReleaseId,
    string OwnerId,
    string WorkerRevisionId,
    IReadOnlyList<JobDeclaration> Declarations)
{
    /// <summary>
    /// Gets the deterministic hash of the normalized declaration snapshot.
    /// </summary>
    public string ContentHash => JobCatalogHash.ComputeSnapshot(this);
}

/// <summary>
/// Projects an active immutable declaration together with its independent operator policy.
/// </summary>
public sealed record ActiveJobDefinition
{
    public required string SchedulerScopeKey { get; init; }
    public required string ReleaseId { get; init; }
    public long ActivationEpoch { get; init; }
    public required string OwnerId { get; init; }
    public required string WorkerRevisionId { get; init; }
    public required JobDeclaration Declaration { get; init; }
    public required JobPolicy Policy { get; init; }
    public string JobRevisionId => JobCatalogHash.ComputeJobRevision(OwnerId, WorkerRevisionId, Declaration);

    /// <summary>
    /// Gets the operator-visible configuration produced from the immutable declaration and sticky policy overrides.
    /// </summary>
    public EffectiveJobConfiguration EffectiveConfiguration => Policy.Overrides.Resolve(Declaration);

    /// <summary>
    /// Gets whether the persisted policy overrides were last reviewed against an earlier immutable job revision.
    /// Policies without overrides never report drift.
    /// </summary>
    public bool IsPolicyReviewOutdated =>
        Policy.Overrides.HasAnyOverride
        && !string.Equals(Policy.ReviewedAgainstJobRevisionId, JobRevisionId, StringComparison.Ordinal);

    /// <summary>
    /// Gets whether the effective operator policy prevents new automatic or triggered admission.
    /// </summary>
    public bool IsDisabled => EffectiveConfiguration.IsDisabled;

    /// <summary>
    /// Captures the exact effective configuration as a durable execution template. Later retry, timeout, display, or
    /// schedule policy edits do not change work that has already entered the queue.
    /// </summary>
    public JobExecutionTemplate CreateExecutionTemplate()
    {
        var effective = EffectiveConfiguration;
        return new JobExecutionTemplate
        {
            Revision = new JobRevisionIdentity
            {
                SchedulerScopeKey = SchedulerScopeKey,
                CatalogReleaseId = ReleaseId,
                ActivationEpoch = ActivationEpoch,
                OwnerKey = OwnerId,
                WorkerRevisionId = WorkerRevisionId,
                JobRevisionId = JobRevisionId,
                JobKey = Declaration.JobKey
            },
            AppliedPolicyRevision = Policy.ConcurrencyStamp,
            JobName = effective.JobName,
            JobType = Declaration.JobType,
            JobArgsKey = Declaration.JobArgsKey,
            MaxConcurrency = effective.MaxConcurrency,
            RetryCount = effective.RetryCount,
            MaxExecutionTimeout = effective.MaxExecutionTimeout
        };
    }
}

/// <summary>
/// Identifies the active release and the durable epochs observed by catalog consumers.
/// </summary>
public sealed record JobCatalogVersion(
    string SchedulerScopeKey,
    string? ActiveReleaseId,
    string? DesiredReleaseId,
    long LastSeenDeploymentGeneration,
    long DesiredIntentEpoch,
    long ActiveIntentEpoch,
    long ActivationEpoch,
    long ChangeEpoch,
    long PublicationEpoch)
{
    public bool IsTransitionInProgress => DesiredIntentEpoch != ActiveIntentEpoch;
}

/// <summary>
/// Records one immutable activation decision, including explicit rollback activations.
/// </summary>
/// <remarks>
/// The execution counts are the aggregate cutover audit. Activation does not append per-execution history entries;
/// queued retirement and running cancellation requests are represented by their durable execution fields instead.
/// Recurring cursors from earlier activation epochs are removed atomically with this decision.
/// </remarks>
public sealed record JobCatalogActivation(
    string SchedulerScopeKey,
    string ReleaseId,
    long ActivationEpoch,
    long DesiredIntentEpoch,
    DateTimeOffset ActivatedAtUtc,
    bool IsExplicitReactivation,
    int RetiredQueuedExecutionCount,
    int RunningCancellationRequestCount);

/// <summary>
/// Reports whether one expected owner has published its immutable declaration snapshot.
/// </summary>
public sealed record JobCatalogOwnerPublicationStatus(
    string OwnerId,
    string WorkerRevisionId,
    bool IsPublished,
    string? ContentHash);

/// <summary>
/// Exposes desired-release convergence independently from active-catalog refresh state.
/// </summary>
public sealed record JobCatalogPublicationStatus(
    JobCatalogVersion Version,
    string? DesiredManifestHash,
    IReadOnlyList<JobCatalogOwnerPublicationStatus> Owners)
{
    public IReadOnlyList<string> MissingOwnerIds => Owners
        .Where(static owner => !owner.IsPublished)
        .Select(static owner => owner.OwnerId)
        .ToArray();
}

/// <summary>
/// Provides a consistent projection of one active catalog version.
/// </summary>
public sealed record JobCatalogSnapshot(
    JobCatalogVersion Version,
    IReadOnlyList<ActiveJobDefinition> Definitions);

/// <summary>
/// Selects the stable ordering used for catalog and operational-summary queries.
/// </summary>
public enum JobCatalogSortField
{
    /// <summary>
    /// Sort by the operator-facing job name.
    /// </summary>
    JobName,

    /// <summary>
    /// Sort by the stable logical job key.
    /// </summary>
    JobKey,

    /// <summary>
    /// Sort by the source owner identity.
    /// </summary>
    OwnerId,

    /// <summary>
    /// Sort by recurring or triggered job type.
    /// </summary>
    JobType,

    /// <summary>
    /// Sort by the effective disabled policy.
    /// </summary>
    IsDisabled
}

/// <summary>
/// Defines a bounded active-catalog query with stable server-side ordering.
/// </summary>
public sealed record JobCatalogQuery
{
    /// <summary>
    /// Gets an optional exact logical job key filter.
    /// </summary>
    public string? JobKey { get; init; }

    public string? OwnerId { get; init; }
    public string? SearchText { get; init; }
    public JobType? JobType { get; init; }
    public bool? IsDisabled { get; init; }

    /// <summary>
    /// Gets the selected server-side sort field.
    /// </summary>
    public JobCatalogSortField SortField { get; init; } = JobCatalogSortField.JobName;

    /// <summary>
    /// Gets whether results are returned in descending order.
    /// </summary>
    public bool SortDescending { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public enum ReleaseStageStatus
{
    StagedAsDesired,
    StagedAsSuperseded,
    Duplicate
}

public sealed record ReleaseStageResult(
    ReleaseStageStatus Status,
    string ReleaseId,
    string ContentHash,
    JobCatalogVersion Version);

public enum OwnerSnapshotPublishStatus
{
    Published,
    Duplicate
}

public sealed record OwnerSnapshotPublishResult(
    OwnerSnapshotPublishStatus Status,
    string ReleaseId,
    string OwnerId,
    string ContentHash);

public enum JobCatalogActivationStatus
{
    Activated,
    AlreadyActive,
    NotReady,
    NotDesired
}

public sealed record JobCatalogActivationResult(
    JobCatalogActivationStatus Status,
    JobCatalogVersion Version,
    IReadOnlyList<string> MissingOwnerIds,
    JobCatalogActivation? Activation = null);
