using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel;

/// <summary>
/// 数据通道类
/// 封装了数据管道，提供统一的访问和控制接口
/// 作为DataChannelCentral的管理单元
/// </summary>
/// <param name="pipeline">数据管道实例</param>
public class DataChannel(DataPipeline pipeline)
{
    /// <summary>
    /// 获取数据管道
    /// 包含数据传输和处理的核心逻辑
    /// </summary>
    public DataPipeline Pipe { get; } = pipeline;
    
    /// <summary>
    /// 获取数据通道的唯一标识符
    /// 与管道ID保持一致
    /// </summary>
    public string Id => Pipe.Id;
    
    /// <summary>
    /// 重新初始化数据通道
    /// 当通道需要重置或重新连接时调用
    /// </summary>
    /// <returns>初始化结果，包含成功状态和可能的错误信息</returns>
    public async Task<Res> ReInitialize(CancellationToken cancellationToken = default)
    {
        return await Pipe.InitAsync(cancellationToken);
    }

    /// <summary>
    /// 从内部端点发送数据，经过转换中间件（若有）处理后由内部端点接收
    /// </summary>
    /// <param name="data">要发送的数据</param>
    public async Task SendDataFromInnerAsync(object data)
    {
        await Pipe.SendDataAsync(new DataContext(EDataSource.Inner, data));
    }

    /// <summary>
    /// 从外部端点发送数据，经过转换中间件（若有）处理后由外部端点接收
    /// </summary>
    /// <param name="data">要发送的数据</param>
    public async Task SendDataFromOuterAsync(object data)
    {
        await Pipe.SendDataAsync(new DataContext(EDataSource.Outer, data));
    }
}