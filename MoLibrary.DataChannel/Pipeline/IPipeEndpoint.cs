using BuildingBlocksPlatform.DataChannel.Interfaces;

namespace BuildingBlocksPlatform.DataChannel.Pipeline;

public interface IPipeEndpoint : IWantAccessPipeline, IPipeComponent
{
    /// <summary>
    /// 接收管道数据
    /// </summary>
    public Task ReceiveDataAsync(DataContext data);

    /// <summary>
    /// 入口类型
    /// </summary>
    public EDataSource EntranceType { get; internal set; }
}