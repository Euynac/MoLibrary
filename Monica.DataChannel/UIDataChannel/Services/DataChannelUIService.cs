using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.DataChannel.UIDataChannel.Models;
using Monica.Core.Results;

namespace Monica.DataChannel.UIDataChannel.Services;

/// <summary>
/// Provides the UI service surface for managing DataChannels.
/// </summary>
/// <remarks>
/// The primary constructor receives the dependencies required by the UI service.
/// </remarks>
/// <param name="manager">Manager that exposes registered DataChannels.</param>
/// <param name="logger">Logger used for auditing and error reporting.</param>
public class DataChannelUIService(IDataChannelManager manager, ILogger<DataChannelUIService> logger)
{

    /// <summary>
    /// Retrieves the status snapshot for every registered DataChannel.
    /// </summary>
    /// <returns>A list of channel status models.</returns>
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
    /// Reinitializes the specified DataChannel pipeline.
    /// </summary>
    /// <param name="id">Identifier of the DataChannel to reinitialize.</param>
    /// <param name="cancellationToken">Token that can cancel the operation.</param>
    /// <returns>The operation result.</returns>
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
    /// Retrieves recent exception details for the given DataChannel.
    /// </summary>
    /// <param name="id">Identifier of the DataChannel whose exceptions are requested.</param>
    /// <param name="count">Maximum number of exceptions to return.</param>
    /// <returns>Exception metadata for the channel.</returns>
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
    /// Builds an aggregate summary of exception statistics across all DataChannels.
    /// </summary>
    /// <returns>The aggregated exception summary.</returns>
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
    /// Clears the exception history for the specified DataChannel.
    /// </summary>
    /// <param name="id">Identifier of the DataChannel.</param>
    /// <returns>The operation result.</returns>
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
    /// Fetches a middleware instance by name from a DataChannel.
    /// </summary>
    /// <param name="channelId">Identifier of the DataChannel that owns the middleware.</param>
    /// <param name="middlewareName">Name of the middleware to retrieve.</param>
    /// <returns>The requested middleware instance, if found.</returns>
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
