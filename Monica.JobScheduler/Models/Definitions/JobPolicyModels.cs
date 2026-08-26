using System.Text.Json.Serialization;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.Models.Definitions;

/// <summary>
/// Represents an explicit nullable description override.
/// </summary>
/// <remarks>
/// A <see langword="null"/> wrapper inherits the declaration description. A present wrapper whose
/// <see cref="Value"/> is <see langword="null"/> explicitly clears that description.
/// </remarks>
public sealed record JobDescriptionOverride
{
    /// <summary>
    /// Gets the operator-supplied description, or <see langword="null"/> to explicitly clear it.
    /// </summary>
    public string? Value { get; init; }
}

/// <summary>
/// Represents an explicit nullable UTC schedule boundary override.
/// </summary>
/// <remarks>
/// A <see langword="null"/> wrapper inherits the corresponding declaration boundary. A present wrapper whose
/// <see cref="Value"/> is <see langword="null"/> explicitly removes that boundary.
/// </remarks>
public sealed record JobScheduleBoundaryOverride
{
    /// <summary>
    /// Gets the replacement UTC boundary, or <see langword="null"/> to remove the declared boundary.
    /// </summary>
    public DateTimeOffset? Value { get; init; }
}

/// <summary>
/// Holds operator-owned recurring schedule fields. The host-configured timezone is intentionally absent and always
/// remains owned by <see cref="JobDeclaration.TimeZoneId"/>.
/// </summary>
public sealed record JobScheduleOverride
{
    /// <summary>
    /// Gets the replacement Cron expression, or <see langword="null"/> to inherit the declaration expression.
    /// </summary>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the optional explicit start-boundary override. A wrapper with a null value removes the boundary.
    /// </summary>
    public JobScheduleBoundaryOverride? StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the optional explicit end-boundary override. A wrapper with a null value removes the boundary.
    /// </summary>
    public JobScheduleBoundaryOverride? EndTimeUtc { get; init; }

    /// <summary>
    /// Gets whether at least one schedule field overrides its declaration value.
    /// </summary>
    [JsonIgnore]
    public bool HasAnyOverride =>
        CronExpression is not null
        || StartTimeUtc is not null
        || EndTimeUtc is not null;

    internal JobScheduleOverride? Normalize()
    {
        var normalized = this with
        {
            CronExpression = CronExpression is null ? null : NormalizeCronExpression(CronExpression),
            StartTimeUtc = StartTimeUtc is null
                ? null
                : StartTimeUtc with { Value = StartTimeUtc.Value?.ToUniversalTime() },
            EndTimeUtc = EndTimeUtc is null
                ? null
                : EndTimeUtc with { Value = EndTimeUtc.Value?.ToUniversalTime() }
        };
        return normalized.HasAnyOverride ? normalized : null;
    }

    internal static string NormalizeCronExpression(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return string.Join(' ', expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// Holds only operator-owned overrides for one owner-scoped job. Null override properties inherit immutable
/// declaration or scheduler defaults and therefore remain sticky without copying code-owned values into policy state.
/// </summary>
public sealed record JobPolicyOverrides
{
    /// <summary>
    /// Gets the enabled-state override. <see langword="null"/> inherits
    /// <see cref="JobDeclaration.IsDisabledByDefault"/>.
    /// </summary>
    public bool? DisabledOverride { get; init; }

    /// <summary>
    /// Gets the operator-facing name override. <see langword="null"/> inherits
    /// <see cref="JobDeclaration.JobName"/>.
    /// </summary>
    public string? DisplayNameOverride { get; init; }

    /// <summary>
    /// Gets the explicit nullable description override. A null wrapper inherits the declaration description.
    /// </summary>
    public JobDescriptionOverride? DescriptionOverride { get; init; }

    /// <summary>
    /// Gets the owner-scoped claim and recurring-admission capacity override.
    /// </summary>
    public int? MaxConcurrencyOverride { get; init; }

    /// <summary>
    /// Gets the retry-count override applied to newly admitted executions.
    /// </summary>
    public int? RetryCountOverride { get; init; }

    /// <summary>
    /// Gets the per-attempt timeout override applied to newly admitted executions.
    /// </summary>
    public TimeSpan? MaxExecutionTimeoutOverride { get; init; }

    /// <summary>
    /// Gets the recurring schedule override. It never contains or changes the configured timezone.
    /// </summary>
    public JobScheduleOverride? ScheduleOverride { get; init; }

    /// <summary>
    /// Gets the retained terminal-execution count override. A null value inherits the scheduler default of 100;
    /// zero disables count-based retention, and a positive value keeps that many newest terminal executions.
    /// </summary>
    public int? MaxRetainedHistoryRecords { get; init; }

    /// <summary>
    /// Gets the retention-age override in days. A null value inherits the scheduler default of no age limit.
    /// </summary>
    public int? MaxRetentionDays { get; init; }

    /// <summary>
    /// Gets whether any declaration or scheduler default is explicitly overridden.
    /// </summary>
    [JsonIgnore]
    public bool HasAnyOverride =>
        DisabledOverride is not null
        || DisplayNameOverride is not null
        || DescriptionOverride is not null
        || MaxConcurrencyOverride is not null
        || RetryCountOverride is not null
        || MaxExecutionTimeoutOverride is not null
        || ScheduleOverride?.HasAnyOverride == true
        || MaxRetainedHistoryRecords is not null
        || MaxRetentionDays is not null;

    internal JobPolicyOverrides Normalize()
    {
        return this with
        {
            DisplayNameOverride = DisplayNameOverride?.Trim(),
            DescriptionOverride = DescriptionOverride is null
                ? null
                : DescriptionOverride with
                {
                    Value = string.IsNullOrWhiteSpace(DescriptionOverride.Value)
                        ? null
                        : DescriptionOverride.Value.Trim()
                },
            ScheduleOverride = ScheduleOverride?.Normalize()
        };
    }

    internal void Validate(
        JobDeclaration declaration,
        JobPolicyOverrides? currentOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (DisplayNameOverride is not null)
        {
            if (string.IsNullOrWhiteSpace(DisplayNameOverride))
            {
                throw new ArgumentException(
                    "The display-name override cannot be empty.",
                    nameof(DisplayNameOverride));
            }

            if (DisplayNameOverride.Length > JobDeclaration.JOB_NAME_MAX_LENGTH)
            {
                throw new ArgumentException(
                    $"The display-name override cannot exceed {JobDeclaration.JOB_NAME_MAX_LENGTH} characters.",
                    nameof(DisplayNameOverride));
            }
        }

        if (DescriptionOverride?.Value is { } description
            && description.Length > JobDeclaration.DESCRIPTION_MAX_LENGTH)
        {
            throw new ArgumentException(
                $"The description override cannot exceed {JobDeclaration.DESCRIPTION_MAX_LENGTH} characters.",
                nameof(DescriptionOverride));
        }

        if (ScheduleOverride?.CronExpression is { } cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                throw new ArgumentException(
                    "The Cron-expression override cannot be empty.",
                    nameof(ScheduleOverride));
            }

            if (cronExpression.Length > JobDeclaration.CRON_EXPRESSION_MAX_LENGTH)
            {
                throw new ArgumentException(
                    $"The Cron-expression override cannot exceed {JobDeclaration.CRON_EXPRESSION_MAX_LENGTH} characters.",
                    nameof(ScheduleOverride));
            }
        }

        if (MaxConcurrencyOverride is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrencyOverride),
                MaxConcurrencyOverride,
                "Maximum concurrency must be greater than zero.");
        }

        if (RetryCountOverride is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryCountOverride),
                RetryCountOverride,
                "Retry count cannot be negative.");
        }

        if (MaxExecutionTimeoutOverride is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxExecutionTimeoutOverride),
                timeout,
                "Execution timeout must be greater than zero.");
        }

        if (MaxRetainedHistoryRecords is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRetainedHistoryRecords),
                MaxRetainedHistoryRecords,
                "History retention count cannot be negative.");
        }

        if (MaxRetentionDays is < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRetentionDays),
                MaxRetentionDays,
                "History retention age must be greater than zero.");
        }

        if (declaration.JobType == JobType.Triggered
            && ScheduleOverride?.HasAnyOverride == true
            && ScheduleOverride != currentOverrides?.ScheduleOverride)
        {
            throw new ArgumentException(
                "A recurring schedule override cannot be introduced or changed while the job is triggered. "
                + "An unchanged dormant override may be retained, or it may be reset.",
                nameof(ScheduleOverride));
        }

        Resolve(declaration).Schedule?.Validate();
    }

    internal EffectiveJobConfiguration Resolve(JobDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        RecurringScheduleDefinition? schedule = null;
        if (declaration.JobType == JobType.Recurring)
        {
            var scheduleOverride = ScheduleOverride;
            schedule = new RecurringScheduleDefinition
            {
                CronExpression = scheduleOverride?.CronExpression ?? declaration.CronExpression!,
                TimeZoneId = declaration.TimeZoneId!,
                StartTimeUtc = scheduleOverride?.StartTimeUtc is { } start
                    ? start.Value?.ToUniversalTime()
                    : declaration.StartTimeUtc?.ToUniversalTime(),
                EndTimeUtc = scheduleOverride?.EndTimeUtc is { } end
                    ? end.Value?.ToUniversalTime()
                    : declaration.EndTimeUtc?.ToUniversalTime()
            };
        }

        return new EffectiveJobConfiguration
        {
            JobName = DisplayNameOverride ?? declaration.JobName,
            Description = DescriptionOverride is null ? declaration.Description : DescriptionOverride.Value,
            IsDisabled = DisabledOverride ?? declaration.IsDisabledByDefault,
            MaxConcurrency = MaxConcurrencyOverride ?? declaration.MaxConcurrency,
            RetryCount = RetryCountOverride ?? declaration.RetryCount,
            MaxExecutionTimeout = MaxExecutionTimeoutOverride ?? declaration.MaxExecutionTimeout,
            Schedule = schedule,
            MaxRetainedHistoryRecords = MaxRetainedHistoryRecords ?? JobPolicy.DEFAULT_MAX_RETAINED_HISTORY_RECORDS,
            MaxRetentionDays = MaxRetentionDays
        };
    }
}

/// <summary>
/// Holds operator-owned behavior for one owner-scoped job independently of the immutable code declaration.
/// </summary>
public sealed record JobPolicy
{
    /// <summary>
    /// Defines the scheduler-owned count-retention default used when no operator override exists.
    /// </summary>
    public const int DEFAULT_MAX_RETAINED_HISTORY_RECORDS = 100;

    /// <summary>
    /// Gets the explicit operator overrides.
    /// </summary>
    public required JobPolicyOverrides Overrides { get; init; }

    /// <summary>
    /// Gets the opaque optimistic-concurrency fence rotated after every successful operator save. It is captured by
    /// future execution templates and gates concurrent policy editors.
    /// </summary>
    public required string ConcurrencyStamp { get; init; }

    /// <summary>
    /// Gets when the policy was persisted in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>
/// Replaces all operator-owned overrides when the supplied policy revision remains current.
/// </summary>
public sealed record JobPolicyChange
{
    /// <summary>
    /// Gets the complete replacement override set. Use record <c>with</c> expressions for single-field edits.
    /// </summary>
    public required JobPolicyOverrides Overrides { get; init; }

    /// <summary>
    /// Gets the policy revision observed by the caller.
    /// </summary>
    public required string ExpectedConcurrencyStamp { get; init; }
}

/// <summary>
/// Projects the runtime behavior produced by an immutable declaration and its operator policy overrides.
/// </summary>
public sealed record EffectiveJobConfiguration
{
    /// <summary>
    /// Gets the effective operator-facing job name.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets the effective optional job description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether new automatic and triggered admission is disabled.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the effective owner-scoped capacity for new claims and recurring admission.
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Gets the retry count captured by newly admitted executions.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the maximum attempt duration captured by newly admitted executions.
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; init; }

    /// <summary>
    /// Gets the effective recurring schedule, or <see langword="null"/> for a triggered job. Its timezone always
    /// comes from the immutable declaration.
    /// </summary>
    public RecurringScheduleDefinition? Schedule { get; init; }

    /// <summary>
    /// Gets the effective terminal-history count. Zero disables count-based retention.
    /// </summary>
    public int MaxRetainedHistoryRecords { get; init; }

    /// <summary>
    /// Gets the effective retention age in days, or <see langword="null"/> for no age limit.
    /// </summary>
    public int? MaxRetentionDays { get; init; }
}
