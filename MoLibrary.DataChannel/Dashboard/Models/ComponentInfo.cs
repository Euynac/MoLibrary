using MoLibrary.DataChannel.Pipeline;
using MoLibrary.DataChannel.BuildInMiddlewares;

namespace MoLibrary.DataChannel.Dashboard.Models;

/// <summary>
/// 管道组件信息
/// </summary>
public class ComponentInfo(IPipeComponent component)
{
    /// <summary>
    /// 组件类型
    /// </summary>
    public EPipeComponentType Type { get; } = GetPipeComponentType(component.GetType());
    
    /// <summary>
    /// 组件名称
    /// </summary>
    public string Name => component.GetType().Name;
    
    /// <summary>
    /// 组件元数据
    /// </summary>
    public object Metadata => component.GetMetadata();

    /// <summary>
    /// 信息展示字典（仅对信息展示中间件有效）
    /// </summary>
    public IReadOnlyDictionary<string, object>? InfoDictionary => 
        component is PipeInfoDisplayMiddlewareBase infoMiddleware ? infoMiddleware.GetInfoDictionary() : null;

    /// <summary>
    /// 获取管道组件类型
    /// </summary>
    /// <param name="type">组件类型</param>
    /// <returns>管道组件类型</returns>
    public static EPipeComponentType GetPipeComponentType(Type type)
    {
        if (type.IsAssignableTo(typeof(IPipeEndpoint)))
        {
            return EPipeComponentType.Endpoint;
        }
        
        // 优先检查信息展示中间件，因为它继承自监控中间件
        if (type.IsAssignableTo(typeof(PipeInfoDisplayMiddlewareBase)))
        {
            return EPipeComponentType.InfoDisplayMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeMonitorMiddleware)))
        {
            return EPipeComponentType.MonitorMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeTransformMiddleware)))
        {
            return EPipeComponentType.TransformMiddleware;
        }
        
        if (type.IsAssignableTo(typeof(IPipeEndpointMiddleware)))
        {
            return EPipeComponentType.EndpointMiddleware;
        }
        
        // 默认返回基础中间件类型
        return EPipeComponentType.BaseMiddleware;
    }
}