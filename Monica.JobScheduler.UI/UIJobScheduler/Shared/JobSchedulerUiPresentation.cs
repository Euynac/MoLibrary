using Monica.JobScheduler.Models.Execution;
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

    internal static string ShortIdentity(string? value, int length = 12)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value ?? "—";
        }

        return value[..length];
    }

    internal static string GetJobKeyLabel(string jobKey) => jobKey.Split('.').LastOrDefault() ?? jobKey;
}
