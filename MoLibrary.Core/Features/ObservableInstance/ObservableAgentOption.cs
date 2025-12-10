namespace MoLibrary.Core.Features.ObservableInstance;

/// <summary>
/// Configuration options for ObservableAgent instances
/// </summary>
public class ObservableAgentOption
{
    /// <summary>
    /// Instance identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum history size
    /// If null, uses global default
    /// </summary>
    public int? MaxHistorySize { get; set; }

    /// <summary>
    /// Instance name (optional)
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Instance type (optional)
    /// </summary>
    public Type? InstanceType { get; set; }

    /// <summary>
    /// Instance key (optional, for keyed instances)
    /// </summary>
    public string? InstanceKey { get; set; }

    /// <summary>
    /// Group ID (optional, for grouping related instances)
    /// </summary>
    public string? GroupId { get; set; }
}
