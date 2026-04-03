namespace Monica.Core.ObjectMapping.Models;

/// <summary>
/// Describes a single registered object mapping.
/// </summary>
public class ObjectMapperInfo
{
    /// <summary>
    /// Fully qualified source type name.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified destination type name.
    /// </summary>
    public string DestinationType { get; set; } = string.Empty;

    /// <summary>
    /// Generated mapping expression script.
    /// </summary>
    public string MapExpression { get; set; } = string.Empty;
}
