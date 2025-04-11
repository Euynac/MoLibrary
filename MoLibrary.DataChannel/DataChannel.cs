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
    public async Task<Res> ReInitialize()
    {
        return await Pipe.InitAsync();
    }
}