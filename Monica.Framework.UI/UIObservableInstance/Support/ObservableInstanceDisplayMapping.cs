using Microsoft.Extensions.Logging;
using Monica.Framework.UI.UIObservableInstance.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIObservableInstance.Support;

/// <summary>
/// Provides shared UI display metadata for observable instance health and log states.
/// </summary>
public static class ObservableInstanceDisplayMapping
{
    /// <summary>
    /// Gets the MudBlazor color used to represent a log level.
    /// </summary>
    public static Color GetLogLevelColor(LogLevel? level) =>
        level switch
        {
            LogLevel.Trace => Color.Default,
            LogLevel.Debug => Color.Default,
            LogLevel.Information => Color.Info,
            LogLevel.Warning => Color.Warning,
            LogLevel.Error => Color.Error,
            LogLevel.Critical => Color.Error,
            _ => Color.Default
        };

    /// <summary>
    /// Gets the icon used to represent a log level.
    /// </summary>
    public static string GetLogLevelIcon(LogLevel? level) =>
        level switch
        {
            LogLevel.Trace => Icons.Material.Filled.Code,
            LogLevel.Debug => Icons.Material.Filled.BugReport,
            LogLevel.Information => Icons.Material.Filled.Info,
            LogLevel.Warning => Icons.Material.Filled.Warning,
            LogLevel.Error => Icons.Material.Filled.Error,
            LogLevel.Critical => Icons.Material.Filled.ErrorOutline,
            _ => Icons.Material.Filled.HelpOutline
        };

    /// <summary>
    /// Gets the MudBlazor color used to represent health.
    /// </summary>
    public static Color GetHealthStateColor(HealthState state) =>
        state switch
        {
            HealthState.Healthy => Color.Success,
            HealthState.Unhealthy => Color.Error,
            _ => Color.Default
        };

    /// <summary>
    /// Gets the icon used to represent health.
    /// </summary>
    public static string GetHealthStateIcon(HealthState state) =>
        state switch
        {
            HealthState.Healthy => Icons.Material.Filled.CheckCircle,
            HealthState.Unhealthy => Icons.Material.Filled.Warning,
            _ => Icons.Material.Filled.HelpOutline
        };
}
