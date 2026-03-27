using Microsoft.Extensions.Logging;

namespace Monica.Core.ObservableInstance.Models;

/// <summary>
/// Registration options for observable instance trackers.
/// </summary>
public class ObservableInstanceRegistration
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

    /// <summary>
    /// Optional logger to automatically log state changes based on configured log levels
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Default log level to use when a state is not explicitly mapped.
    /// If null, unmapped states will not be logged automatically.
    /// </summary>
    public LogLevel? DefaultLogLevel { get; set; }
}
