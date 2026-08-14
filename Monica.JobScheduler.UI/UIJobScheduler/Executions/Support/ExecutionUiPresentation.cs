using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.State;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.Executions.Support;

/// <summary>
/// Formats execution evidence consistently across the ledger and detail dialog.
/// </summary>
internal static class ExecutionUiPresentation
{
    private static readonly JsonSerializerOptions INDENTED_JSON_OPTIONS = new()
    {
        WriteIndented = true
    };

    internal static string FormatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, INDENTED_JSON_OPTIONS);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    internal static string ExportDiagnostics(JobExecutionInstance execution, DateTimeOffset observedAtUtc)
    {
        return JsonSerializer.Serialize(
            new { ObservedAtUtc = observedAtUtc, Execution = execution },
            INDENTED_JSON_OPTIONS);
    }

    internal static (string Message, Severity Severity) GetCancellationFeedback(
        ExecutionCancellationUiResult result,
        IStringLocalizer<JobSchedulerResource> localizer)
    {
        if (!result.IsAuthorized)
        {
            return (localizer["Access:DeniedDescription"].Value, Severity.Error);
        }

        if (result.Error is not null)
        {
            return (result.Error, Severity.Error);
        }

        return result.Status switch
        {
            JobCancellationStatus.Cancelled => (localizer["Executions:Cancel:Cancelled"].Value, Severity.Success),
            JobCancellationStatus.CancellationRequested => (localizer["Executions:Cancel:Requested"].Value, Severity.Success),
            JobCancellationStatus.AlreadyTerminal => (localizer["Executions:Cancel:AlreadyTerminal"].Value, Severity.Info),
            JobCancellationStatus.NotFound => (localizer["Executions:Cancel:NotFound"].Value, Severity.Warning),
            _ => (localizer["Executions:Cancel:Failed"].Value, Severity.Error)
        };
    }

    internal static Color GetLogLevelColor(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace or LogLevel.Debug => Color.Default,
        LogLevel.Information => Color.Info,
        LogLevel.Warning => Color.Warning,
        LogLevel.Error or LogLevel.Critical => Color.Error,
        _ => Color.Default
    };
}
