namespace MoLibrary.FrameworkUI.UIObservableInstance.Models;

/// <summary>
/// Statistics model for observable instances
/// </summary>
public class ObservableInstanceStatistics
{
    /// <summary>
    /// Total number of registered instances
    /// </summary>
    public int TotalInstances { get; set; }

    /// <summary>
    /// Number of instances with exceptions
    /// </summary>
    public int InstancesWithExceptions { get; set; }

    /// <summary>
    /// Number of instances without exceptions
    /// </summary>
    public int InstancesWithoutExceptions { get; set; }

    /// <summary>
    /// Total number of state changes across all instances
    /// </summary>
    public int TotalStateChanges { get; set; }

    /// <summary>
    /// Total number of exceptions across all instances
    /// </summary>
    public int TotalExceptions { get; set; }

    /// <summary>
    /// Average number of history entries per instance
    /// </summary>
    public double AverageHistoryPerInstance { get; set; }

    /// <summary>
    /// Top types by instance count
    /// </summary>
    public List<TypeCount> TopTypesByCount { get; set; } = new();

    /// <summary>
    /// Top groups by instance count
    /// </summary>
    public List<GroupCount> TopGroupsByCount { get; set; } = new();
}

/// <summary>
/// Type count statistics
/// </summary>
public class TypeCount
{
    /// <summary>
    /// Type full name
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Type short name
    /// </summary>
    public string TypeShortName { get; set; } = string.Empty;

    /// <summary>
    /// Number of instances of this type
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Group count statistics
/// </summary>
public class GroupCount
{
    /// <summary>
    /// Group ID
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Number of instances in this group
    /// </summary>
    public int Count { get; set; }
}
