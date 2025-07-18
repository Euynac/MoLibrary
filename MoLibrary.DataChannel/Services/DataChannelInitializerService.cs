using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MoLibrary.DataChannel.Services;

/// <summary>
/// 数据通道初始化服务
/// 实现IHostedService接口，在应用程序启动时负责初始化所有注册的数据通道
/// 解决通道初始化与应用程序生命周期同步的问题
/// </summary>
/// <remarks>
/// 初始化数据通道初始化服务的新实例
/// </remarks>
/// <param name="logger">日志记录器实例，用于记录初始化过程中的事件</param>
public class DataChannelInitializerService(ILogger<DataChannelInitializerService>? logger = null) : IHostedService
{

    /// <summary>
    /// 启动初始化过程
    /// 在ASP.NET Core应用程序启动时由主机调用
    /// 遍历所有已注册的数据通道并异步初始化它们
    /// </summary>
    /// <param name="cancellationToken">取消令牌，可用于取消初始化操作</param>
    /// <returns>表示异步初始化操作的任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Parallel.ForEachAsync(DataChannelCentral.Channels,new ParallelOptions{MaxDegreeOfParallelism = 10,CancellationToken = cancellationToken}, async (channel, token) =>
        {
            try
            {
                await channel.Value.Pipe.InitAsync(token);
                logger?.LogInformation("Successfully initialized channel: {ChannelId}", channel.Key);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize channel: {ChannelId}", channel.Key);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务
    /// 在ASP.NET Core应用程序停止时由主机调用
    /// 当前实现仅记录停止事件
    /// </summary>
    /// <param name="cancellationToken">取消令牌，可用于取消停止操作</param>
    /// <returns>表示异步停止操作的任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger?.LogInformation("DataChannelInitializerService is stopping");
        return Task.CompletedTask;
    }
} 