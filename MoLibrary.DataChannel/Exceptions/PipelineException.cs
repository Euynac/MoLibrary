using MoLibrary.Core.ExceptionHandler.ExceptionPool;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Exceptions;

/// <summary>
/// 数据管道异常信息
/// 记录管道运行过程中产生的异常及其来源
/// </summary>
public class PipelineException : PooledException
{
    /// <summary>
    /// 初始化管道异常信息
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    public PipelineException(Exception exception, object source)
        : base(exception, source)
    {
    }

    /// <summary>
    /// 初始化管道异常信息
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    /// <param name="description">异常描述信息</param>
    public PipelineException(Exception exception, object source, string? description)
        : base(exception, source, description)
    {
    }

    /// <summary>
    /// 确定异常来源类型
    /// 添加数据管道特定的类型识别逻辑
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源类型字符串</returns>
    protected override string DetermineSourceType(object source)
    {
        return source switch
        {
            IPipeEndpoint => "Endpoint",
            IPipeEndpointMiddleware => "EndpointMiddleware",
            IPipeTransformMiddleware => "TransformMiddleware",
            IPipeMiddleware => "Middleware",
            _ => base.DetermineSourceType(source)
        };
    }

    /// <summary>
    /// 确定异常来源描述
    /// 添加数据管道特定的描述逻辑
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源描述字符串</returns>
    protected override string DetermineSourceDescription(object source)
    {
        return source switch
        {
            IPipeComponent component => $"{component.GetType().Name} ({component.GetType().GetCleanFullName()})",
            _ => base.DetermineSourceDescription(source)
        };
    }
} 