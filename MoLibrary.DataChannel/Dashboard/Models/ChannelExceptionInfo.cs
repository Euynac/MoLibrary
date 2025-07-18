namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// DataChannel异常信息
/// </summary>
public class ChannelExceptionInfo
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
    public int CurrentExceptions { get; set; }
    
    /// <summary>
    /// 总异常数量
    /// </summary>
    public int TotalExceptions { get; set; }
    
    /// <summary>
    /// 异常池最大大小
    /// </summary>
    public int MaxPoolSize { get; set; }
    
    /// <summary>
    /// 是否有异常
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// 异常详情列表
    /// </summary>
    public List<ExceptionDetailInfo> Exceptions { get; set; } = new();
}

/// <summary>
/// 异常详情信息
/// </summary>
public class ExceptionDetailInfo
{
    /// <summary>
    /// 异常时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 异常源类型
    /// </summary>
    public string SourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常源描述
    /// </summary>
    public string SourceDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常类型
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常堆栈
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;
} 