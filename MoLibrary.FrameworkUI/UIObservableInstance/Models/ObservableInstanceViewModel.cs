using System.Text.Json;
using MoLibrary.Tool.Extensions;
using MudBlazor;

namespace MoLibrary.FrameworkUI.UIObservableInstance.Models;

/// <summary>
/// UI-friendly view model for ObservableAgent instances
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

    #endregion
}
