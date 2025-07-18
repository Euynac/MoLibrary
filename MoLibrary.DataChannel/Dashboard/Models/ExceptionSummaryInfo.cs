namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// 异常统计信息
/// </summary>
public class ExceptionSummaryInfo
{
    /// <summary>
    /// 总Channel数量
    /// </summary>
    public int TotalChannels { get; set; }
    
    /// <summary>
    /// 有异常的Channel数量
    /// </summary>
    public int ChannelsWithExceptions { get; set; }
    
    /// <summary>
    /// 当前总异常数量
    /// </summary>
    public int TotalCurrentExceptions { get; set; }
    
    /// <summary>
    /// 历史总异常数量
    /// </summary>
    public int TotalHistoricalExceptions { get; set; }
    
    /// <summary>
    /// Channel统计信息列表
    /// </summary>
    public List<ChannelSummaryInfo> ChannelSummaries { get; set; } = new();
}

/// <summary>
/// Channel统计信息
/// </summary>
public class ChannelSummaryInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;
    
    /// <summary>
    /// Pipeline ID
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;
    
    /// <summary>
    /// 当前异常数量
    /// </summary>
    public int CurrentExceptionCount { get; set; }
    
    /// <summary>
    /// 总异常数量
    /// </summary>
    public int TotalExceptionCount { get; set; }
    
    /// <summary>
    /// 异常池最大大小
    /// </summary>
    public int MaxPoolSize { get; set; }
    
    /// <summary>
    /// 是否有异常
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// 最新异常时间
    /// </summary>
    public DateTime? LatestException { get; set; }
} 