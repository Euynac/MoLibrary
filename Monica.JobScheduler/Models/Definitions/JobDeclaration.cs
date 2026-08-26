namespace Monica.JobScheduler.Models.Definitions;

using Monica.JobScheduler.Models.Execution;

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
    /// Gets the owner-scoped logical identity of the job.
    /// </summary>
    public required string JobKey { get; init; }
    public string? JobArgsKey { get; init; }
    public required string JobName { get; init; }
    public string? Description { get; init; }
    public JobType JobType { get; init; }
    /// <summary>
    /// Gets the owner-scoped job capacity. It limits active leases and determines when overlapping recurring
    /// occurrences are recorded as skipped instead of queued.
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
