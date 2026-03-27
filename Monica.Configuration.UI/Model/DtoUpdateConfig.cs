using System.Text.Json.Nodes;

namespace Monica.Configuration.UI.Model;

public class DtoUpdateConfig
{
    /// <summary>
    /// AppID corresponding to the configuration item/configuration class
    /// </summary>
    public required string AppId { get; set; }
    /// <summary>
    /// Configuration item/configuration class Key
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// Configuration item/configuration class modification value
    /// </summary>
    public JsonNode? Value { get; set; }
}

public class DtoUpdateConfigRes
{
    /// <summary>
    /// AppID corresponding to the corresponding configuration item/configuration class
    /// </summary>
    public string? AppId { get; set; }
    /// <summary>
    /// Corresponding configuration item/configuration class Key
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// Configuration class title
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// Final configuration class/configuration item value
    /// </summary>
    public required JsonNode? NewValue { get; set; }
    /// <summary>
    /// Original configuration class/configuration item value
    /// </summary>
    public required JsonNode? OldValue { get; set; }
   
}