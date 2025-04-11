using MoLibrary.DataChannel.Interfaces;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 管道端点接口
/// 定义数据管道的入口和出口点，负责数据的接收和处理
/// 实现此接口的组件可作为数据流的源或目标
/// </summary>
public interface IPipeEndpoint : IWantAccessPipeline, IPipeComponent
{
    /// <summary>
    /// 接收管道数据
    /// 当数据到达端点时由管道调用此方法处理数据
    /// </summary>
    /// <param name="data">要处理的数据上下文</param>
    /// <returns>表示异步操作的任务</returns>
    public Task ReceiveDataAsync(DataContext data);

    /// <summary>
    /// 入口类型
    /// 定义此端点的数据方向（内部或外部）
    /// </summary>
    public EDataSource EntranceType { get; internal set; }
}