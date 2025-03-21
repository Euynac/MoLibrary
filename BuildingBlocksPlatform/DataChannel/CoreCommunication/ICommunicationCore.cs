using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunication;

public interface ICommunicationCore : IPipeEndpoint
{
    /// <summary>
    /// 初始化通信核心
    /// </summary>
    /// <returns></returns>
    Task InitAsync();

    /// <summary>
    /// 结束通信核心
    /// </summary>
    /// <returns></returns>
    Task DisposeAsync();

    /// <summary>
    /// 支持的通信方向
    /// </summary>
    /// <returns></returns>
    EConnectionDirection SupportedConnectionDirection();

    /// <summary>
    /// 发送管道数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    Task SendDataAsync(DataContext data);
}