using Microsoft.Extensions.Logging;

namespace Monica.Framework.UI.UIObservableInstance.Models;

/// <summary>
/// Filter model for observable instances
/// </summary>
public class ObservableInstanceFilter
{
    /// <summary>
    /// Search text (search by InstanceId/Name)
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Filter by group ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Filter by health state (null = all)
    /// </summary>
    public HealthState? HealthStateFilter { get; set; }

    /// <summary>
    /// Filter by specific log levels (null or empty = all)
    /// </summary>
    public HashSet<LogLevel>? LogLevels { get; set; }

    /// <summary>
    /// Quick filter: show only Critical and Error
    /// </summary>
    public bool ShowCriticalErrorOnly { get; set; }

    /// <summary>
    /// Quick filter: show only unhealthy instances
    /// </summary>
    public bool ShowUnhealthyOnly { get; set; }

    /// <summary>
    /// Filter by registration date range (start)
    /// </summary>
    public DateTime? RegisteredFrom { get; set; }

    /// <summary>
    /// Filter by registration date range (end)
    /// </summary>
    public DateTime? RegisteredTo { get; set; }

    /// <summary>
    /// Whether any filter is active
    /// </summary>
    public bool HasAnyFilter =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        !string.IsNullOrWhiteSpace(GroupId) ||
        HealthStateFilter.HasValue ||
        LogLevels?.Any() == true ||
        ShowCriticalErrorOnly ||
        ShowUnhealthyOnly ||
        RegisteredFrom.HasValue ||
        RegisteredTo.HasValue;

    /// <summary>
    /// Count of active filters
    /// </summary>
    public int ActiveFilterCount
    {
        get
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(SearchText)) count++;
            if (!string.IsNullOrWhiteSpace(GroupId)) count++;
            if (HealthStateFilter.HasValue) count++;
            if (LogLevels?.Any() == true) count++;
            if (ShowCriticalErrorOnly) count++;
            if (ShowUnhealthyOnly) count++;
            if (RegisteredFrom.HasValue || RegisteredTo.HasValue) count++;
            return count;
        }
    }

    /// <summary>
    /// Clear all filters
    /// </summary>
    public void Clear()
    {
        SearchText = null;
        GroupId = null;
        HealthStateFilter = null;
        LogLevels = null;
        ShowCriticalErrorOnly = false;
        ShowUnhealthyOnly = false;
        RegisteredFrom = null;
        RegisteredTo = null;
    }
}
