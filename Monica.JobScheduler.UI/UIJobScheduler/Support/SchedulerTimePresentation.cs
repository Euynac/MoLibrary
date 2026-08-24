using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Models.Analytics;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Localization;
using Monica.Modules;

namespace Monica.JobScheduler.UI.UIJobScheduler.Support;

/// <summary>
/// Presents scheduler instants and elapsed durations consistently in the configured scheduler timezone.
/// </summary>
/// <remarks>
/// Persisted instants remain UTC. This service owns the UI projection into
/// <see cref="ModuleJobSchedulerOption.CronTimeZone" /> so Blazor Server never silently falls back to the server's
/// local timezone.
/// </remarks>
internal sealed class SchedulerTimePresentation(
    IOptions<ModuleJobSchedulerOption> schedulerOptions,
    IStringLocalizer<JobSchedulerResource> localizer)
{
    private readonly TimeZoneInfo _timeZone = schedulerOptions.Value.CronTimeZone;

    /// <summary>
    /// Gets the configured scheduler timezone identifier shown once by the shared workspace.
    /// </summary>
    internal string TimeZoneId => _timeZone.Id;

    /// <summary>
    /// Gets the platform-provided descriptive timezone name for supporting UI metadata.
    /// </summary>
    internal string TimeZoneDisplayName => _timeZone.DisplayName;

    /// <summary>
    /// Gets the UTC instant at which the configured scheduler calendar day begins.
    /// </summary>
    /// <remarks>
    /// A rare daylight-saving transition can skip or repeat midnight. Skipped wall times advance to the first valid
    /// minute, while repeated wall times select the earliest represented instant so the complete scheduler day remains
    /// inside the analytics range.
    /// </remarks>
    internal DateTimeOffset GetStartOfSchedulerDayUtc(DateTimeOffset value)
    {
        var wallTime = DateTime.SpecifyKind(ToSchedulerWallTime(value).Date, DateTimeKind.Unspecified);
        while (_timeZone.IsInvalidTime(wallTime))
        {
            wallTime = wallTime.AddMinutes(1);
        }

        if (_timeZone.IsAmbiguousTime(wallTime))
        {
            return _timeZone.GetAmbiguousTimeOffsets(wallTime)
                .Select(offset => new DateTimeOffset(wallTime, offset).ToUniversalTime())
                .Min();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(wallTime, _timeZone), TimeSpan.Zero);
    }

    /// <summary>
    /// Formats one durable UTC instant as scheduler-zone wall time with millisecond precision.
    /// </summary>
    internal string FormatTimestamp(DateTimeOffset? value) => value is { } instant
        ? ToSchedulerWallTime(instant).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.CurrentCulture)
        : "—";

    /// <summary>
    /// Projects an absolute instant into the configured scheduler-zone wall clock.
    /// </summary>
    internal DateTime ToSchedulerWallTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, _timeZone).DateTime;

    /// <summary>
    /// Interprets an unspecified wall-clock value in the configured scheduler timezone and returns its UTC instant.
    /// </summary>
    internal DateTimeOffset ConvertSchedulerWallTimeToUtc(DateTime wallTime)
    {
        var unspecifiedWallTime = DateTime.SpecifyKind(wallTime, DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(unspecifiedWallTime, _timeZone);
        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    /// <summary>
    /// Tries to interpret a scheduler wall time, rejecting clock values skipped by a daylight-saving transition.
    /// </summary>
    internal bool TryConvertSchedulerWallTimeToUtc(DateTime wallTime, out DateTimeOffset utcValue)
    {
        var unspecifiedWallTime = DateTime.SpecifyKind(wallTime, DateTimeKind.Unspecified);
        if (_timeZone.IsInvalidTime(unspecifiedWallTime))
        {
            utcValue = default;
            return false;
        }

        utcValue = ConvertSchedulerWallTimeToUtc(unspecifiedWallTime);
        return true;
    }

    /// <summary>
    /// Formats a compact analytics-axis label after projecting the fixed UTC bucket into scheduler wall time.
    /// </summary>
    internal string FormatBucketLabel(
        DateTimeOffset value,
        JobExecutionAnalyticsBucketSize bucketSize)
    {
        var schedulerTime = ToSchedulerWallTime(value);
        return bucketSize switch
        {
            JobExecutionAnalyticsBucketSize.Hour => schedulerTime.ToString("HH:mm", CultureInfo.CurrentCulture),
            JobExecutionAnalyticsBucketSize.Day => schedulerTime.ToString("MM-dd", CultureInfo.CurrentCulture),
            _ => FormatTimestamp(value)
        };
    }

    /// <summary>
    /// Formats elapsed time as milliseconds below one second and decimal seconds otherwise.
    /// </summary>
    internal string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
        {
            return "—";
        }

        var value = duration.Value < TimeSpan.Zero ? TimeSpan.Zero : duration.Value;
        if (value.TotalSeconds >= 1)
        {
            return FormatDurationUnit("Common:Duration:Seconds", value.TotalSeconds, "F2");
        }

        return FormatDurationUnit(
            "Common:Duration:Milliseconds",
            Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero),
            "F0");
    }

    /// <summary>
    /// Calculates and formats the elapsed time represented by one durable execution snapshot.
    /// </summary>
    internal string FormatExecutionDuration(JobExecutionInstance execution, DateTimeOffset observedAtUtc)
    {
        var duration = GetExecutionDuration(execution, observedAtUtc);
        if (duration is null)
        {
            return "—";
        }

        var formatted = FormatDuration(duration);
        return execution.CompletedAtUtc is null
            ? localizer["Common:RunningDuration", formatted]
            : formatted;
    }

    /// <summary>
    /// Calculates elapsed execution time, using the caller's observation instant for active work.
    /// </summary>
    internal static TimeSpan? GetExecutionDuration(
        JobExecutionInstance execution,
        DateTimeOffset observedAtUtc)
    {
        if (execution.StartedAtUtc is not { } startedAtUtc)
        {
            return null;
        }

        var end = execution.CompletedAtUtc ?? observedAtUtc;
        return end < startedAtUtc ? TimeSpan.Zero : end - startedAtUtc;
    }

    private string FormatDurationUnit(string key, double value, string format) =>
        localizer[key, value.ToString(format, CultureInfo.CurrentCulture)];
}
