using Monica.JobScheduler.Models;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

/// <summary>
/// Job status color service
/// Provides a unified mapping of job status to MudBlazor colors
/// </summary>
public class JobStateColorResolver
{
    /// <summary>
    /// Get the MudBlazor color corresponding to the job status
    /// </summary>
    /// <param name="state">Job status</param>
    /// <returns>Corresponding MudBlazor color</returns>
    public Color GetStateColor(JobState state)
    {
        return state switch
        {
            JobState.Succeeded => Color.Success,
            JobState.Failed => Color.Error,
            JobState.Terminated => Color.Error,
            JobState.Cancelled => Color.Warning,
            JobState.Processing => Color.Primary,
            JobState.Enqueued => Color.Info,
            JobState.Scheduled => Color.Default,
            JobState.Skipped => Color.Surface,
            _ => Color.Default
        };
    }

    /// <summary>
    /// Gets the theme token corresponding to the job status for CSS-backed visualizations.
    /// </summary>
    /// <param name="state">Job status</param>
    /// <returns>A MudBlazor palette custom property.</returns>
    public static string GetStateColorToken(JobState state)
    {
        return state switch
        {
            JobState.Succeeded => "var(--mud-palette-success)",
            JobState.Failed or JobState.Terminated => "var(--mud-palette-error)",
            JobState.Cancelled => "var(--mud-palette-warning)",
            JobState.Processing => "var(--mud-palette-primary)",
            JobState.Enqueued => "var(--mud-palette-info)",
            JobState.Scheduled => "var(--mud-palette-secondary)",
            JobState.Skipped => "var(--mud-palette-text-disabled)",
            _ => "var(--mud-palette-text-secondary)"
        };
    }
}
