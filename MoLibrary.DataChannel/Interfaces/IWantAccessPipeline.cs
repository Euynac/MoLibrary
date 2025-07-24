using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// 管道访问接口
/// 允许组件（如中间件和端点）访问其所属的管道实例
/// 实现此接口的组件在管道初始化时会自动注入管道引用
/// </summary>
public interface IWantAccessPipeline
{
    /// <summary>
    /// 管道实例
    /// 组件所属的数据管道引用，用于访问管道功能和其他组件
    /// </summary>
    public DataPipeline Pipe { get; set; }

    /// <summary>
    /// 收集异常信息到管道的异常池
    /// 为开发者提供便捷的异常收集接口，无需直接访问Pipe属性
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="source">异常来源对象，可选参数，默认为当前实例</param>
    void CollectException(Exception exception, object? source = null)
    {
        Pipe?.CollectException(exception, source ?? this);
    }

    /// <summary>
    /// 收集异常信息到管道的异常池（带业务描述）
    /// 为开发者提供便捷的异常收集接口，无需直接访问Pipe属性
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <param name="businessDescription">业务描述信息</param>
    /// <param name="source">异常来源对象，可选参数，默认为当前实例</param>
    void CollectException(Exception exception, string? businessDescription, object? source = null)
    {
        Pipe?.CollectException(exception, source ?? this, businessDescription);
    }
}