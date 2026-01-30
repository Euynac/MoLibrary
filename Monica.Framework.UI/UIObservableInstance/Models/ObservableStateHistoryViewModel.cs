using System.Text.Json;
using Microsoft.Extensions.Logging;
using Monica.Tool.Extensions;
using MudBlazor;

namespace Monica.Framework.UI.UIObservableInstance.Models;

/// <summary>
/// UI-friendly view model for ObservableStateHistory entries
/// </summary>
public class ObservableStateHistoryViewModel
{
    #region Core Properties

    /// <summary>
    /// Timestamp when this state change occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Previous state before this change
    /// </summary>
    public object? PreviousState { get; set; }

    /// <summary>
    /// Current state after this change
    /// </summary>
    public object? CurrentState { get; set; }

    /// <summary>
    /// Descriptive message about this state change
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception associated with this state change (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Log level associated with this state change (if mapped)
    /// </summary>
    public LogLevel? LogLevel { get; set; }

    #endregion

    #region Flags

    /// <summary>
    /// Whether this is an exception entry
    /// </summary>
    public bool IsException => Exception != null;

    /// <summary>
    /// Whether this is a state transition (state changed)
    /// </summary>
    public bool IsStateTransition => !Equals(PreviousState, CurrentState);

    #endregion

    #region Display Properties

    /// <summary>
    /// Formatted previous state
    /// </summary>
    public string PreviousStateDisplay => FormatStateDisplay(PreviousState);

    /// <summary>
    /// Formatted current state
    /// </summary>
    public string CurrentStateDisplay => FormatStateDisplay(CurrentState);

    /// <summary>
    /// Formatted timestamp
    /// </summary>
    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Relative time display ("2 minutes ago" format)
    /// </summary>
    public string RelativeTimeDisplay
    {
        get
        {
            var elapsed = DateTime.UtcNow - Timestamp;

            if (elapsed.TotalSeconds < 60)
                return $"{(int)elapsed.TotalSeconds}秒前";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}分钟前";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}小时前";
            if (elapsed.TotalDays < 7)
                return $"{(int)elapsed.TotalDays}天前";

            return TimestampDisplay;
        }
    }

    /// <summary>
    /// Exception message (if exception exists)
    /// </summary>
    public string ExceptionMessage => Exception?.Message ?? string.Empty;

    /// <summary>
    /// Exception type name (if exception exists)
    /// </summary>
    public string ExceptionType => Exception?.GetType().GetCleanName() ?? string.Empty;

    /// <summary>
    /// Exception stack trace (if exception exists)
    /// </summary>
    public string ExceptionStackTrace => Exception?.StackTrace ?? string.Empty;

    /// <summary>
    /// Exception summary for display (type + message)
    /// </summary>
    public string ExceptionSummary
    {
        get
        {
            if (Exception == null) return string.Empty;

            var message = Exception.Message;
            if (message.Length > 100)
                message = message[..100] + "...";

            return $"{ExceptionType}: {message}";
        }
    }

    /// <summary>
    /// Log level text for display
    /// </summary>
    public string LogLevelText => LogLevel?.ToString() ?? "Unknown";

    /// <summary>
    /// Log level color
    /// </summary>
    public Color LogLevelColor => MapLogLevelToColor(LogLevel);

    /// <summary>
    /// Log level dot color (CSS variable)
    /// </summary>
    public string LogLevelDotColor => MapLogLevelToCssColor(LogLevel);

    /// <summary>
    /// Log level icon
    /// </summary>
    public string LogLevelIcon => MapLogLevelToIcon(LogLevel);

    #endregion

    #region Color Properties

    /// <summary>
    /// Color based on log level (preferred) or entry type fallback
    /// </summary>
    public Color HistoryEntryColor
    {
        get
        {
            // Prefer log level color if available
            if (LogLevel.HasValue)
                return MapLogLevelToColor(LogLevel);

            // Fallback to exception/transition logic
            return (IsException, IsStateTransition) switch
            {
                (true, true) => Color.Warning,   // State change with exception
                (true, false) => Color.Error,    // Exception without state change
                (false, true) => Color.Info,     // Normal state change
                (false, false) => Color.Default  // Message only
            };
        }
    }

    /// <summary>
    /// Icon for this history entry
    /// </summary>
    public string HistoryEntryIcon => (IsException, IsStateTransition) switch
    {
        (true, true) => Icons.Material.Filled.WarningAmber,     // State change with exception
        (true, false) => Icons.Material.Filled.Error,           // Exception without state change
        (false, true) => Icons.Material.Filled.SwapHoriz,       // Normal state change
        (false, false) => Icons.Material.Filled.Info           // Message only
    };

    /// <summary>
    /// Entry type description
    /// </summary>
    public string EntryTypeDescription => (IsException, IsStateTransition) switch
    {
        (true, true) => "状态变更（异常）",
        (true, false) => "异常",
        (false, true) => "状态变更",
        (false, false) => "消息"
    };

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
    /// Maps log level to MudBlazor color
    /// </summary>
    private static Color MapLogLevelToColor(Microsoft.Extensions.Logging.LogLevel? level) =>
        level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => Color.Default,
            Microsoft.Extensions.Logging.LogLevel.Debug => Color.Default,
            Microsoft.Extensions.Logging.LogLevel.Information => Color.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => Color.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => Color.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => Color.Error,
            _ => Color.Default
        };

    /// <summary>
    /// Maps log level to CSS variable color
    /// </summary>
    private static string MapLogLevelToCssColor(Microsoft.Extensions.Logging.LogLevel? level) =>
        level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => "var(--mud-palette-text-secondary)",
            Microsoft.Extensions.Logging.LogLevel.Debug => "var(--mud-palette-text-secondary)",
            Microsoft.Extensions.Logging.LogLevel.Information => "var(--mud-palette-info)",
            Microsoft.Extensions.Logging.LogLevel.Warning => "var(--mud-palette-warning)",
            Microsoft.Extensions.Logging.LogLevel.Error => "var(--mud-palette-error)",
            Microsoft.Extensions.Logging.LogLevel.Critical => "var(--mud-palette-error-darken)",
            _ => "var(--mud-palette-text-disabled)"
        };

    /// <summary>
    /// Maps log level to icon
    /// </summary>
    private static string MapLogLevelToIcon(Microsoft.Extensions.Logging.LogLevel? level) =>
        level switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => Icons.Material.Filled.Code,
            Microsoft.Extensions.Logging.LogLevel.Debug => Icons.Material.Filled.BugReport,
            Microsoft.Extensions.Logging.LogLevel.Information => Icons.Material.Filled.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => Icons.Material.Filled.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => Icons.Material.Filled.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => Icons.Material.Filled.ErrorOutline,
            _ => Icons.Material.Filled.HelpOutline
        };

    #endregion
}
