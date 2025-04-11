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
}