namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// 管道组件类型枚举
/// </summary>
public enum EPipeComponentType
{
    /// <summary>
    /// 管道端点类型
    /// </summary>
    Endpoint,
    
    /// <summary>
    /// 管道转换中间件类型
    /// </summary>
    TransformMiddleware,
    
    /// <summary>
    /// 管道端点中间件类型
    /// </summary>
    EndpointMiddleware,
    
    /// <summary>
    /// 管道监控中间件类型
    /// </summary>
    MonitorMiddleware,
    
    /// <summary>
    /// 基础管道中间件类型
    /// </summary>
    BaseMiddleware
}