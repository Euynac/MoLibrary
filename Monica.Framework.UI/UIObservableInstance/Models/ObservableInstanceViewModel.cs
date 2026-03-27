using System.Text.Json;
using Microsoft.Extensions.Logging;
using Monica.Tool.Extensions;
using MudBlazor;

namespace Monica.Framework.UI.UIObservableInstance.Models;

/// <summary>
/// UI-friendly view model for ObservableInstanceTracker instances
/// </summary>
public class ObservableInstanceViewModel
{
    #region Identity

    /// <summary>
    /// Unique instance identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable instance name
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Type of the instance
    /// </summary>
    public Type? InstanceType { get; set; }

    /// <summary>
    /// Instance key for keyed service instances (optional)
    /// </summary>
    public string? InstanceKey { get; set; }

    /// <summary>
    /// Group ID for grouping related instances (optional)
    /// </summary>
    public string? GroupId { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Current state of the instance (can be any type)
    /// </summary>
    public object? CurrentState { get; set; }

    /// <summary>
    /// Timestamp when the current state was entered
    /// </summary>
    public DateTime StateChangedAt { get; set; }

    /// <summary>
    /// Timestamp when this instance was registered
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    #endregion

    #region Statistics

    /// <summary>
    /// Total number of state changes that have occurred
    /// </summary>
    public int TotalStateChanges { get; set; }

    /// <summary>
    /// Total number of exceptions that have occurred
    /// </summary>
    public int TotalExceptions { get; set; }

    /// <summary>
    /// Current number of history entries
    /// </summary>
    public int HistoryCount { get; set; }

    /// <summary>
    /// Count of entries with exceptions
    /// </summary>
    public int ExceptionCount { get; set; }

    /// <summary>
    /// Whether there are any exceptions in history
    /// </summary>
    public bool HasExceptions { get; set; }

    #endregion

    #region Log Level & Health State

    /// <summary>
    /// Current log level for this instance (null if not mapped)
    /// </summary>
    public LogLevel? CurrentLogLevel { get; set; }

    /// <summary>
    /// Whether this instance is unhealthy (Warning/Error/Critical)
    /// </summary>
    public bool IsUnhealthy { get; set; }

    /// <summary>
    /// Computed health state
    /// </summary>
    public HealthState HealthState
    {
        get
        {
            if (CurrentLogLevel == null) return HealthState.Unknown;
            return IsUnhealthy ? HealthState.Unhealthy : HealthState.Healthy;
        }
    }

    #endregion

    #region Display Properties

    /// <summary>
    /// Formatted state value (handles object? with JSON/ToString)
    /// </summary>
    public string StateDisplayText => FormatStateDisplay(CurrentState);

    /// <summary>
    /// Type of current state
    /// </summary>
    public string StateTypeDisplay => CurrentState?.GetType().GetCleanName() ?? "-";

    /// <summary>
    /// Short type name using GetCleanName()
    /// </summary>
    public string InstanceTypeDisplay => InstanceType?.GetCleanName() ?? "Unknown";

    /// <summary>
    /// Full type name using GetCleanFullName()
    /// </summary>
    public string InstanceTypeFullName => InstanceType?.GetCleanFullName() ?? "-";

    /// <summary>
    /// Handles null/empty GroupId gracefully
    /// </summary>
    public string GroupIdDisplay => string.IsNullOrEmpty(GroupId) ? "Ungrouped" : GroupId;

    /// <summary>
    /// Formatted registered timestamp
    /// </summary>
    public string RegisteredAtDisplay => RegisteredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Formatted state changed timestamp
    /// </summary>
    public string StateChangedAtDisplay => StateChangedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Time since RegisteredAt
    /// </summary>
    public string RunningDurationDisplay
    {
        get
        {
            var duration = DateTime.UtcNow - RegisteredAt;
            return FormatDuration(duration);
        }
    }

    /// <summary>
    /// Log level text for display
    /// </summary>
    public string LogLevelText => CurrentLogLevel?.ToString() ?? "Unknown";

    /// <summary>
    /// MudBlazor color for log level
    /// </summary>
    public Color LogLevelColor => MapLogLevelToColor(CurrentLogLevel);

    /// <summary>
    /// CSS variable color for log level dot
    /// </summary>
    public string LogLevelDotColor => MapLogLevelToCssColor(CurrentLogLevel);

    /// <summary>
    /// Icon for log level
    /// </summary>
    public string LogLevelIcon => MapLogLevelToIcon(CurrentLogLevel);

    /// <summary>
    /// Health state text
    /// </summary>
    public string HealthStateText => HealthState switch
    {
        HealthState.Healthy => "健康",
        HealthState.Unhealthy => "不健康",
        _ => "未知"
    };

    /// <summary>
    /// MudBlazor color for health state
    /// </summary>
    public Color HealthStateColor => HealthState switch
    {
        HealthState.Healthy => Color.Success,
        HealthState.Unhealthy => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// CSS variable color for health state dot
    /// </summary>
    public string HealthStateDotColor => HealthState switch
    {
        HealthState.Healthy => "var(--mud-palette-success)",
        HealthState.Unhealthy => "var(--mud-palette-error)",
        _ => "var(--mud-palette-text-disabled)"
    };

    /// <summary>
    /// Icon for health state
    /// </summary>
    public string HealthStateIcon => HealthState switch
    {
        HealthState.Healthy => Icons.Material.Filled.CheckCircle,
        HealthState.Unhealthy => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.HelpOutline
    };

    #endregion

    #region Color Properties

    /// <summary>
    /// Color based on exception status
    /// </summary>
    public Color ExceptionStatusColor => HasExceptions ? Color.Error : Color.Success;

    /// <summary>
    /// Exception status badge text
    /// </summary>
    public string ExceptionStatusText => HasExceptions ? $"{ExceptionCount} 异常" : "正常";

    #endregion

    #region Computed Properties

    /// <summary>
    /// Whether this instance has a group
    /// </summary>
    public bool HasGroup => !string.IsNullOrEmpty(GroupId);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Formats state value for display (handles object? types)
    /// </summary>
    private static string FormatStateDisplay(object? state)
    {
        if (state == null) return "-";

        // Handle primitive types and common types
        if (state is string or int or long or bool or DateTime or Guid)
            return state.ToString() ?? "-";

        // Handle enums
        if (state.GetType().IsEnum)
            return state.ToString() ?? "-";

        // For complex objects, serialize to JSON (max 200 chars)
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3
            });
            return json.Length > 200 ? json[..200] + "..." : json;
        }
        catch
        {
            return state.GetType().GetCleanName();  // Fallback to type name
        }
    }

    /// <summary>
    /// Formats duration to human-readable string
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}天 {duration.Hours}小时";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}小时 {duration.Minutes}分钟";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}分钟 {duration.Seconds}秒";
        return $"{(int)duration.TotalSeconds}秒";
    }

    /// <summary>
    /// Maps log level to MudBlazor color
    /// </summary>
    private static Color MapLogLevelToColor(LogLevel? level) =>
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
    /// Maps log level to CSS variable color
    /// </summary>
    private static string MapLogLevelToCssColor(LogLevel? level) =>
        level switch
        {
            LogLevel.Trace => "var(--mud-palette-text-secondary)",
            LogLevel.Debug => "var(--mud-palette-text-secondary)",
            LogLevel.Information => "var(--mud-palette-info)",
            LogLevel.Warning => "var(--mud-palette-warning)",
            LogLevel.Error => "var(--mud-palette-error)",
            LogLevel.Critical => "var(--mud-palette-error-darken)",
            _ => "var(--mud-palette-text-disabled)"
        };

    /// <summary>
    /// Maps log level to icon
    /// </summary>
    private static string MapLogLevelToIcon(LogLevel? level) =>
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

    #endregion
}
