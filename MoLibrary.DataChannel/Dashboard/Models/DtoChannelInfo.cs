namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// DataChannel信息传输对象
/// </summary>
public class DtoChannelInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Channel名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Channel描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}