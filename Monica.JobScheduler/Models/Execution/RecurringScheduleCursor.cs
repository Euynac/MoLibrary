using Monica.JobScheduler.Utils;

namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Captures the code-owned recurring schedule needed to calculate occurrences after a scheduler restart.
/// </summary>
/// <remarks>
/// Cron evaluation uses the persisted timezone identifier. The optional boundaries are absolute UTC instants, so a
/// different control-plane replica evaluates the same immutable schedule deterministically.
/// </remarks>
public sealed record RecurringScheduleDefinition
{
    /// <summary>
    /// Gets the code-owned cron expression.
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Gets the immutable system time-zone identifier used to evaluate the cron expression.
    /// </summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// Gets the optional inclusive UTC start boundary.
    /// </summary>
    public DateTimeOffset? StartTimeUtc { get; init; }

    /// <summary>
    /// Gets the optional inclusive UTC end boundary.
    /// </summary>
    public DateTimeOffset? EndTimeUtc { get; init; }

    /// <summary>
    /// Calculates the first occurrence strictly after the supplied UTC instant while honoring the schedule's
    /// inclusive start and end boundaries.
    /// </summary>
    /// <param name="afterUtc">The exclusive instant after which to search. Non-UTC offsets are normalized to UTC.</param>
    /// <returns>The next occurrence in UTC, or <see langword="null"/> when the schedule is exhausted.</returns>
    internal DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc)
    {
        var normalizedAfterUtc = afterUtc.ToUniversalTime();
        var searchFrom = StartTimeUtc is { } start && normalizedAfterUtc < start
            ? start.AddTicks(-1)
            : normalizedAfterUtc;
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var occurrence = CronHelper.Parse(CronExpression).GetNextOccurrence(searchFrom, timezone);
        if (occurrence is null)
        {
            return null;
        }

        var result = occurrence.Value.ToUniversalTime();
        return EndTimeUtc is { } end && result > end ? null : result;
    }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CronExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(TimeZoneId);
        _ = CronHelper.Parse(CronExpression);
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"Recurring schedule timezone '{TimeZoneId}' is not available.",
                nameof(TimeZoneId),
                exception);
        }

        if (StartTimeUtc is { } startBoundary && startBoundary.Offset != TimeSpan.Zero
            || EndTimeUtc is { } endBoundary && endBoundary.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recurring schedule boundaries must use the UTC offset.");
        }

        if (StartTimeUtc is { } start && EndTimeUtc is { } end && end < start)
        {
            throw new ArgumentException("Recurring schedule end time cannot precede its start time.");
        }
    }
}

/// <summary>
/// Identifies one durable recurring schedule cursor within an activation epoch.
/// </summary>
public sealed record RecurringScheduleCursorKey
{
    /// <summary>
    /// Gets the scheduler scope containing the cursor.
    /// </summary>
    public required string SchedulerScopeKey { get; init; }

    /// <summary>
    /// Gets the activation epoch that owns the cursor.
    /// </summary>
    public long ActivationEpoch { get; init; }

    /// <summary>
    /// Gets the immutable job revision represented by the cursor.
    /// </summary>
    public required string JobRevisionId { get; init; }

    internal void Validate()
    {
        JobSchedulerIdentity.ValidateStandard(SchedulerScopeKey, nameof(SchedulerScopeKey));
        JobSchedulerIdentity.ValidateHash(JobRevisionId, nameof(JobRevisionId));

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
/// Synchronizes the durable cursor for a recurring declaration with an observed catalog version.
/// </summary>
/// <remarks>
/// A newer change epoch may suspend a cursor or ask the store to resume it. The store calculates an active cursor's
/// initial occurrence from the durable catalog activation boundary, so delayed control-plane synchronization cannot
/// skip already-due work. Resuming an explicitly suspended cursor starts after the store's authoritative current time
/// instead of replaying disabled time. When the desired suspension state has not changed, synchronization preserves
/// the cursor's current occurrence so unrelated policy changes cannot skip or duplicate work.
/// </remarks>
public sealed record RecurringScheduleSynchronization
{
    /// <summary>
    /// Gets the recurring job's immutable execution template.
    /// </summary>
    public required JobExecutionTemplate Template { get; init; }

    /// <summary>
    /// Gets the code-owned cron expression and optional execution boundaries.
    /// </summary>
    public required RecurringScheduleDefinition Schedule { get; init; }

    /// <summary>
    /// Gets the catalog change epoch observed while deriving the desired suspension state.
    /// </summary>
    public long ChangeEpoch { get; init; }

    /// <summary>
    /// Gets whether recurring materialization must be suspended by the current operator policy.
    /// </summary>
    public bool IsSuspended { get; init; }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Template);
        Template.Validate();
        ArgumentNullException.ThrowIfNull(Schedule);
        Schedule.Validate();
        if (ChangeEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ChangeEpoch),
                ChangeEpoch,
                "A catalog change epoch must be greater than zero.");
        }

        if (Template.JobType != JobType.Recurring)
        {
            throw new ArgumentException("Only recurring jobs can own recurring schedule cursors.", nameof(Template));
        }
    }
}

/// <summary>
/// Describes how a catalog-version synchronization affected a recurring cursor.
/// </summary>
public enum RecurringScheduleSynchronizationStatus
{
    /// <summary>
    /// A cursor was created for the active activation.
    /// </summary>
    Created,

    /// <summary>
    /// A newer catalog change epoch updated the cursor.
    /// </summary>
    Updated,

    /// <summary>
    /// The cursor had already observed the same catalog change epoch.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The request observed an older catalog change epoch and was ignored.
    /// </summary>
    StaleChangeEpoch,

    /// <summary>
    /// The supplied execution template no longer belongs to the active activation.
    /// </summary>
    InactiveActivation
}

/// <summary>
/// Reports the durable result of synchronizing one recurring cursor.
/// </summary>
public sealed record RecurringScheduleSynchronizationResult
{
    /// <summary>
    /// Gets the synchronization outcome.
    /// </summary>
    public RecurringScheduleSynchronizationStatus Status { get; init; }

    /// <summary>
    /// Gets the current cursor, or <see langword="null"/> when the activation is no longer active and no cursor exists.
    /// </summary>
    public RecurringScheduleCursor? Cursor { get; init; }
}

/// <summary>
/// Provides an immutable snapshot of a persistent recurring schedule cursor.
/// </summary>
public sealed record RecurringScheduleCursor
{
    /// <summary>
    /// Gets the cursor identity.
    /// </summary>
    public required RecurringScheduleCursorKey Key { get; init; }

    /// <summary>
    /// Gets the immutable execution template used for each materialized occurrence.
    /// </summary>
    public required JobExecutionTemplate Template { get; init; }

    /// <summary>
    /// Gets the code-owned schedule used to calculate the next occurrence.
    /// </summary>
    public required RecurringScheduleDefinition Schedule { get; init; }

    /// <summary>
    /// Gets the next occurrence in UTC, or <see langword="null"/> when no future occurrence exists.
    /// </summary>
    public DateTimeOffset? NextOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets the latest catalog change epoch applied to this cursor.
    /// </summary>
    public long LastSynchronizedChangeEpoch { get; init; }

    /// <summary>
    /// Gets whether materialization is suspended by the current operator policy.
    /// </summary>
    public bool IsSuspended { get; init; }

    /// <summary>
    /// Gets the compare-and-swap version used to fence stale control-plane replicas.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Gets when the cursor was last changed in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>
/// Requests atomic materialization and cursor advancement for one due recurring occurrence.
/// </summary>
public sealed record RecurringOccurrenceMaterialization
{
    /// <summary>
    /// Gets the cursor being advanced.
    /// </summary>
    public required RecurringScheduleCursorKey CursorKey { get; init; }

    /// <summary>
    /// Gets the expected cursor version observed by a control-plane replica.
    /// </summary>
    public long ExpectedVersion { get; init; }

    /// <summary>
    /// Gets the exact occurrence expected at the cursor.
    /// </summary>
    public DateTimeOffset ExpectedOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets the following occurrence, or <see langword="null"/> when the schedule is exhausted.
    /// </summary>
    public DateTimeOffset? NextOccurrenceUtc { get; init; }

    /// <summary>
    /// Gets the deterministic execution identifier assigned to this occurrence.
    /// </summary>
    public required string InstanceId { get; init; }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(CursorKey);
        CursorKey.Validate();
        JobSchedulerIdentity.ValidateStandard(InstanceId, nameof(InstanceId));

        if (ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExpectedVersion),
                ExpectedVersion,
                "A cursor version must be greater than zero.");
        }

        if (ExpectedOccurrenceUtc == default)
        {
            throw new ArgumentException("The expected occurrence is required.", nameof(ExpectedOccurrenceUtc));
        }

        if (NextOccurrenceUtc is { } next && next <= ExpectedOccurrenceUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(NextOccurrenceUtc),
                next,
                "The next occurrence must be later than the occurrence being materialized.");
        }
    }
}

/// <summary>
/// Describes the result of recurring occurrence materialization.
/// </summary>
public enum RecurringMaterializationStatus
{
    /// <summary>
    /// The occurrence was inserted and the cursor advanced atomically.
    /// </summary>
    Materialized,

    /// <summary>
    /// The cursor version or expected occurrence no longer matched.
    /// </summary>
    StaleCursor,

    /// <summary>
    /// The cursor's activation epoch is no longer active.
    /// </summary>
    InactiveActivation,

    /// <summary>
    /// The cursor does not exist.
    /// </summary>
    CursorNotFound,

    /// <summary>
    /// The expected occurrence has not become due yet.
    /// </summary>
    NotDue,

    /// <summary>
    /// The cursor is suspended or the current active policy disabled the job during materialization.
    /// </summary>
    Suspended
}

/// <summary>
/// Reports the durable outcome of recurring occurrence materialization.
/// </summary>
public sealed record RecurringMaterializationResult
{
    /// <summary>
    /// Gets the materialization status.
    /// </summary>
    public RecurringMaterializationStatus Status { get; init; }

    /// <summary>
    /// Gets the materialized execution when one exists.
    /// </summary>
    public JobExecutionInstance? Execution { get; init; }

    /// <summary>
    /// Gets the resulting cursor when the cursor exists.
    /// </summary>
    public RecurringScheduleCursor? Cursor { get; init; }
}
