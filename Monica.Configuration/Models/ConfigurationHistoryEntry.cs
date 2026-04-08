using System.Text.Json.Nodes;

namespace Monica.Configuration.Models;

public class ConfigurationHistoryEntry
{
    /// <summary>
    /// record title
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// ProjectId
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// {Configuration class Key}:{Configuration item Key}
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// Configuration value Value
    /// </summary>
    public JsonNode? OldValue { get; set; }

    /// <summary>
    /// Configure new values
    /// </summary>
    public JsonNode? NewValue { get; set; }
    /// <summary>
    /// Configure update time
    /// </summary>
    public DateTime ModificationTime { get; set; }
    /// <summary>
    /// Configure update source ID
    /// </summary>
    public string? ModifierId { get; set; }
    /// <summary>
    /// Configure update source name
    /// </summary>
    public string? Username { get; set; }
    /// <summary>
    /// Configuration version
    /// </summary>
    public required string Version { get; set; }
}
