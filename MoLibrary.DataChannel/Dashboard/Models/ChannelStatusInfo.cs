namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// DataChannel状态信息
/// </summary>
public class ChannelStatusInfo
{
    /// <summary>
    /// Channel ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// 中间件列表
    /// </summary>
    public List<ComponentInfo> Middlewares { get; set; } = new();
    
    /// <summary>
    /// 内部端点
    /// </summary>
    public ComponentInfo InnerEndpoint { get; set; } = null!;
    
    /// <summary>
    /// 外部端点
    /// </summary>
    public ComponentInfo OuterEndpoint { get; set; } = null!;
    
    /// <summary>
    /// 是否不可用
    /// </summary>
    public bool IsNotAvailable { get; set; }
    
    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized { get; set; }
    
    /// <summary>
    /// 是否正在初始化
    /// </summary>
    public bool IsInitializing { get; set; }
    
    /// <summary>
    /// 是否有异常
    /// </summary>
    public bool HasExceptions { get; set; }
    
    /// <summary>
    /// 当前异常数量
    /// </summary>
    public int ExceptionCount { get; set; }
    
    /// <summary>
    /// 总异常数量
    /// </summary>
    public int TotalExceptionCount { get; set; }
} 