using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池记录基类
/// 记录异常信息及其来源，可被继承以添加特定领域的逻辑
/// </summary>
public class PooledException
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
    /// 业务描述信息
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 初始化异常池记录
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    public PooledException(Exception exception, object source)
    {
        Timestamp = DateTime.Now;
        Exception = exception;
        Source = source;

        // 确定来源类型和描述
        SourceType = DetermineSourceType(source);
        SourceDescription = DetermineSourceDescription(source);
    }

    /// <summary>
    /// 初始化异常池记录
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    /// <param name="description">异常描述信息</param>
    public PooledException(Exception exception, object source, string? description)
    {
        Timestamp = DateTime.Now;
        Exception = exception;
        Source = source;
        Description = description;

        // 确定来源类型和描述
        SourceType = DetermineSourceType(source);
        SourceDescription = DetermineSourceDescription(source);
    }

    /// <summary>
    /// 确定异常来源类型
    /// 子类可以重写此方法以添加特定领域的类型识别逻辑
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源类型字符串</returns>
    protected virtual string DetermineSourceType(object source)
    {
        return source.GetType().Name;
    }

    /// <summary>
    /// 确定异常来源描述
    /// 子类可以重写此方法以添加特定领域的描述逻辑
    /// </summary>
    /// <param name="source">来源对象</param>
    /// <returns>来源描述字符串</returns>
    protected virtual string DetermineSourceDescription(object source)
    {
        return $"{source.GetType().Name} ({source.GetType().GetCleanFullName()})";
    }
}
