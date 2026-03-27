namespace Monica.DataChannel.UIDataChannel.Models;

/// <summary>
/// Transport DTO for exposing DataChannel metadata to the UI.
/// </summary>
public class DtoChannelInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the channel.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the channel.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the channel is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}
