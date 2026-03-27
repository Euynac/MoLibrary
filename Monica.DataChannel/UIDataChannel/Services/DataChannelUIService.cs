using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.DataChannel.UIDataChannel.Models;
using Monica.Tool.Results;

namespace Monica.DataChannel.UIDataChannel.Services;

/// <summary>
/// DataChannel服务，提供DataChannel管理的核心业务逻辑
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="manager">DataChannel管理器</param>
/// <param name="logger">日志记录器</param>
public class DataChannelUIService(IDataChannelManager manager, ILogger<DataChannelUIService> logger)
{

    /// <summary>
    /// 获取所有DataChannel的状态信息
    /// </summary>
    /// <returns>DataChannel状态信息列表</returns>
    public async Task<Res<List<ChannelStatusInfo>>> GetChannelsStatusAsync()
    {
        try
        {
            var channels = manager.FetchAll().Select(channel => new ChannelStatusInfo
            {
                Id = channel.Id,
                Middlewares = channel.Pipe.GetMiddlewares().Select(m => new ComponentInfo(m)).ToList(),
                InnerEndpoint = new ComponentInfo(channel.Pipe.InnerEndpoint),
                OuterEndpoint = new ComponentInfo(channel.Pipe.OuterEndpoint),
                IsNotAvailable = channel.Pipe.IsNotAvailable,
                IsInitialized = channel.Pipe.IsInitialized,
                IsInitializing = channel.Pipe.IsInitializing,
                HasExceptions = channel.Pipe.HasExceptions,
                ExceptionCount = channel.Pipe.ObservableTracker.ExceptionCount,
                TotalExceptionCount = channel.Pipe.ObservableTracker.TotalExceptions
            }).ToList();

            return await Task.FromResult(Res.Ok(channels));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取DataChannel状态信息失败");
            return Res.Fail($"获取DataChannel状态信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新初始化指定的DataChannel
    /// </summary>
    /// <param name="id">DataChannel ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<Res> ReInitializeChannelAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            await channel.ReInitialize(cancellationToken);
            return Res.Ok("重新初始化成功");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "重新初始化DataChannel失败，ID: {Id}", id);
            return Res.Fail($"重新初始化DataChannel失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定DataChannel的异常信息
    /// </summary>
    /// <param name="id">DataChannel ID</param>
    /// <param name="count">获取的异常数量</param>
    /// <returns>异常信息</returns>
    public async Task<Res<ChannelExceptionInfo>> GetChannelExceptionsAsync(string id, int count = 10)
    {
        try
        {
            var channel = manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            var exceptions = channel.Pipe.GetRecentExceptions(count);

            var result = new ChannelExceptionInfo
            {
                ChannelId = id,
                PipelineId = channel.Pipe.ObservableTracker.InstanceId,
                CurrentExceptions = channel.Pipe.ObservableTracker.ExceptionCount,
                TotalExceptions = channel.Pipe.ObservableTracker.TotalStateChanges,
                MaxPoolSize = channel.Pipe.ObservableTracker.MaxHistorySize,
                HasExceptions = channel.Pipe.HasExceptions,
                Exceptions = exceptions.Select(ex => new ExceptionDetailInfo
                {
                    Timestamp = ex.Timestamp,
                    SourceType = "Unknown",
                    SourceDescription = ex.Message,
                    Description = ex.Message,
                    ExceptionType = ex.Exception?.GetType().Name ?? "Unknown",
                    Message = ex.Exception?.GetMessageRecursively() ?? ex.Message,
                    StackTrace = ex.Exception?.ToString() ?? string.Empty
                }).ToList()
            };

            return await Task.FromResult(Res.Ok(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取DataChannel异常信息失败，ID: {Id}", id);
            return Res.Fail($"获取DataChannel异常信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有DataChannel的异常统计信息
    /// </summary>
    /// <returns>异常统计信息</returns>
    public async Task<Res<ExceptionSummaryInfo>> GetExceptionSummaryAsync()
    {
        try
        {
            var channels = manager.FetchAll();
            
            var summary = new ExceptionSummaryInfo
            {
                TotalChannels = channels.Count,
                ChannelsWithExceptions = channels.Count(c => c.Pipe.HasExceptions),
                TotalCurrentExceptions = channels.Sum(c => c.Pipe.ObservableTracker.ExceptionCount),
                TotalHistoricalExceptions = channels.Sum(c => c.Pipe.ObservableTracker.TotalStateChanges),
                ChannelSummaries = channels.Select(channel => new ChannelSummaryInfo
                {
                    ChannelId = channel.Id,
                    PipelineId = channel.Pipe.ObservableTracker.InstanceId,
                    CurrentExceptionCount = channel.Pipe.ObservableTracker.ExceptionCount,
                    TotalExceptionCount = channel.Pipe.ObservableTracker.TotalStateChanges,
                    MaxPoolSize = channel.Pipe.ObservableTracker.MaxHistorySize,
                    HasExceptions = channel.Pipe.HasExceptions,
                    LatestException = channel.Pipe.GetRecentExceptions(1).FirstOrDefault()?.Timestamp
                }).ToList()
            };

            return await Task.FromResult(Res.Ok(summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取DataChannel异常统计信息失败");
            return Res.Fail($"获取DataChannel异常统计信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清空指定DataChannel的异常信息
    /// </summary>
    /// <param name="id">DataChannel ID</param>
    /// <returns>操作结果</returns>
    public async Task<Res> ClearChannelExceptionsAsync(string id)
    {
        try
        {
            var channel = manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            channel.Pipe.ObservableTracker.ClearHistory();
            return await Task.FromResult(Res.Ok("异常信息已清空"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清空DataChannel异常信息失败，ID: {Id}", id);
            return Res.Fail($"清空DataChannel异常信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定DataChannel中的特定中间件实例
    /// </summary>
    /// <param name="channelId">DataChannel ID</param>
    /// <param name="middlewareName">中间件名称</param>
    /// <returns>中间件实例</returns>
    public async Task<Res<T?>> GetMiddlewareAsync<T>(string channelId, string middlewareName) where T : class
    {
        try
        {
            var channel = manager.Fetch(channelId);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            var middleware = channel.Pipe.GetMiddlewares()
                .FirstOrDefault(m => m.GetType().Name == middlewareName) as T;

            if (middleware == null)
            {
                return Res.Fail($"未找到名为 {middlewareName} 的中间件");
            }

            return await Task.FromResult(Res.Ok<T?>(middleware));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取中间件实例失败，Channel ID: {ChannelId}, Middleware: {MiddlewareName}", channelId, middlewareName);
            return Res.Fail($"获取中间件实例失败: {ex.Message}");
        }
    }
} 
