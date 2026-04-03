using Monica.JobScheduler.Models;
using Monica.UI.Shell.State;
using MudBlazor;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

/// <summary>
/// Job status color service
/// Provides a unified mapping of job status to MudBlazor colors
/// </summary>
public class JobStateColorResolver(IThemeState themeService)
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
    /// Get the hexadecimal color value corresponding to the job status
    /// Returns the corresponding color based on the current theme and light and dark mode
    /// </summary>
    /// <param name="state">Job status</param>
    /// <returns>Hex color value</returns>
    public string GetStateColorHex(JobState state)
    {
        if(state == JobState.Skipped) return Colors.Gray.Lighten1;
        return themeService.GetColorHex(GetStateColor(state));
    }
}
