using System.Globalization;
using Microsoft.Extensions.Localization;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Models.Operations;
using Monica.JobScheduler.UI.Localization;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared;

/// <summary>
/// Centralizes small, theme-aware scheduler presentation mappings shared by UI components.
/// </summary>
internal static class JobSchedulerUiPresentation
{
    internal static Color GetStateColor(JobExecutionState state) => state switch
    {
        JobExecutionState.Queued => Color.Info,
        JobExecutionState.Running => Color.Primary,
        JobExecutionState.Succeeded => Color.Success,
        JobExecutionState.Failed => Color.Error,
        JobExecutionState.Cancelled => Color.Secondary,
        JobExecutionState.Skipped => Color.Warning,
        _ => Color.Default
    };

    internal static Color GetRecurringScheduleStatusColor(JobRecurringScheduleStatus status) => status switch
    {
        JobRecurringScheduleStatus.Scheduled => Color.Success,
        JobRecurringScheduleStatus.AwaitingSynchronization => Color.Info,
        JobRecurringScheduleStatus.Suspended => Color.Warning,
        JobRecurringScheduleStatus.Exhausted => Color.Secondary,
        _ => Color.Default
    };

    internal static string FormatUtc(DateTimeOffset? value) => value?.ToUniversalTime()
        .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "—";

    internal static string ShortIdentity(string? value, int length = 12)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value ?? "—";
        }

        return value[..length];
    }

    internal static string GetJobKeyLabel(string jobKey) => jobKey.Split('.').LastOrDefault() ?? jobKey;

    internal static string DescribeCron(
        string? expression,
        IStringLocalizer<JobSchedulerResource> localizer)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return localizer["Catalog:Cron:Unavailable"];
        }

        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var offset = fields.Length == 6 ? 1 : 0;
        if (fields.Length is not (5 or 6))
        {
            return localizer["Catalog:Cron:Custom"];
        }

        var minute = fields[offset];
        var hour = fields[offset + 1];
        var day = fields[offset + 2];
        var month = fields[offset + 3];
        var weekday = fields[offset + 4];
        if (day != "*" || month != "*" || weekday != "*")
        {
            return localizer["Catalog:Cron:Custom"];
        }

        if (hour == "*" && minute.StartsWith("*/", StringComparison.Ordinal)
                        && int.TryParse(minute.AsSpan(2), out var minuteInterval)
                        && minuteInterval > 0)
        {
            return localizer["Catalog:Cron:EveryMinutes", minuteInterval];
        }

        if (hour == "*" && minute == "0")
        {
            return localizer["Catalog:Cron:EveryHour"];
        }

        if (int.TryParse(hour, out var dailyHour)
            && int.TryParse(minute, out var dailyMinute)
            && dailyHour is >= 0 and <= 23
            && dailyMinute is >= 0 and <= 59)
        {
            return localizer["Catalog:Cron:EveryDayAt", $"{dailyHour:00}:{dailyMinute:00}"];
        }

        return localizer["Catalog:Cron:Custom"];
    }

    internal static string FormatExecutionDuration(
        JobExecutionInstance execution,
        DateTimeOffset now,
        IStringLocalizer<JobSchedulerResource> localizer)
    {
        if (execution.StartedAtUtc is not { } startedAt)
        {
            return "—";
        }

        var end = execution.CompletedAtUtc ?? now;
        var duration = end > startedAt ? end - startedAt : TimeSpan.Zero;
        var formatted = FormatDuration(duration);
        return execution.CompletedAtUtc is null
            ? localizer["Common:RunningDuration", formatted]
            : formatted;
    }

    internal static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{Math.Max(0, (int)duration.TotalSeconds)}s";
    }
}
