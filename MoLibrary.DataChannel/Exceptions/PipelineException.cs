using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Exceptions;

/// <summary>
/// 数据管道异常信息
/// 记录管道运行过程中产生的异常及其来源
/// </summary>
public class PipelineException
{
    /// <summary>
    /// 异常发生的时间
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// 异常来源对象
    /// </summary>
    public object Source { get; set; }

    /// <summary>
    /// 异常来源类型
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// 异常来源描述
    /// </summary>
    public string SourceDescription { get; set; }

    /// <summary>
    /// 初始化管道异常信息
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    public PipelineException(Exception exception, object source)
    {
        Timestamp = DateTime.Now;
        Exception = exception;
        Source = source;
        
        // 确定来源类型和描述
        SourceType = DetermineSourceType(source);
        SourceDescription = DetermineSourceDescription(source);
    }

    /// <summary>
    /// 确定异常来源类型
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源类型字符串</returns>
    private string DetermineSourceType(object source)
    {
        return source switch
        {
            IPipeEndpoint => "Endpoint",
            IPipeEndpointMiddleware => "EndpointMiddleware",
            IPipeTransformMiddleware => "TransformMiddleware",
            IPipeMiddleware => "Middleware",
            _ => source.GetType().Name
        };
    }

    /// <summary>
    /// 确定异常来源描述
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源描述字符串</returns>
    private string DetermineSourceDescription(object source)
    {
        return source switch
        {
            IPipeComponent component => $"{component.GetType().Name} ({component.GetType().GetCleanFullName()})",
            _ => $"{source.GetType().Name} ({source.GetType().GetCleanFullName()})"
        };
    }
} 