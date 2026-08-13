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
        JobExecutionState.Cancelled => Color.Warning,
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
