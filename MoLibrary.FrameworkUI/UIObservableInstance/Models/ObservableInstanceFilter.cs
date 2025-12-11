namespace MoLibrary.FrameworkUI.UIObservableInstance.Models;

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
    /// Filter by instance type
    /// </summary>
    public Type? InstanceType { get; set; }

    /// <summary>
    /// Filter by group ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Filter by exception status (null = all, true = with exceptions, false = without exceptions)
    /// </summary>
    public bool? HasExceptions { get; set; }

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
        InstanceType != null ||
        !string.IsNullOrWhiteSpace(GroupId) ||
        HasExceptions.HasValue ||
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
            if (InstanceType != null) count++;
            if (!string.IsNullOrWhiteSpace(GroupId)) count++;
            if (HasExceptions.HasValue) count++;
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
        InstanceType = null;
        GroupId = null;
        HasExceptions = null;
        RegisteredFrom = null;
        RegisteredTo = null;
    }
}
