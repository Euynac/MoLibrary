using MoLibrary.DataChannel.Dashboard.Models;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.Tool.MoResponse;
using Microsoft.Extensions.Logging;

namespace MoLibrary.DataChannel.Dashboard.Services;

/// <summary>
/// DataChannel服务，提供DataChannel管理的核心业务逻辑
/// </summary>
public class DataChannelService
{
    private readonly IDataChannelManager _manager;
    private readonly ILogger<DataChannelService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="manager">DataChannel管理器</param>
    /// <param name="logger">日志记录器</param>
    public DataChannelService(IDataChannelManager manager, ILogger<DataChannelService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有DataChannel的状态信息
    /// </summary>
    /// <returns>DataChannel状态信息列表</returns>
    public async Task<Res<List<ChannelStatusInfo>>> GetChannelsStatusAsync()
    {
        try
        {
            var channels = _manager.FetchAll().Select(channel => new ChannelStatusInfo
            {
                Id = channel.Id,
                Middlewares = channel.Pipe.GetMiddlewares().Select(m => new ComponentInfo(m)).ToList(),
                InnerEndpoint = new ComponentInfo(channel.Pipe.InnerEndpoint),
                OuterEndpoint = new ComponentInfo(channel.Pipe.OuterEndpoint),
                IsNotAvailable = channel.Pipe.IsNotAvailable,
                IsInitialized = channel.Pipe.IsInitialized,
                IsInitializing = channel.Pipe.IsInitializing,
                HasExceptions = channel.Pipe.HasExceptions,
                ExceptionCount = channel.Pipe.ExceptionPool.Count,
                TotalExceptionCount = channel.Pipe.ExceptionPool.TotalExceptionCount
            }).ToList();

            return await Task.FromResult(Res.Ok(channels));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取DataChannel状态信息失败");
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
            var channel = _manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            var result = await channel.ReInitialize(cancellationToken);
            return result.Code == ResponseCode.Ok ? Res.Ok("重新初始化成功") : Res.Fail(result.Message ?? "重新初始化失败");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新初始化DataChannel失败，ID: {Id}", id);
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
            var channel = _manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            var exceptions = channel.Pipe.ExceptionPool.GetRecentExceptions(count);
            
            var result = new ChannelExceptionInfo
            {
                ChannelId = id,
                PipelineId = channel.Pipe.ExceptionPool.PipelineId,
                CurrentExceptions = channel.Pipe.ExceptionPool.Count,
                TotalExceptions = channel.Pipe.ExceptionPool.TotalExceptionCount,
                MaxPoolSize = channel.Pipe.ExceptionPool.MaxSize,
                HasExceptions = channel.Pipe.HasExceptions,
                Exceptions = exceptions.Select(ex => new ExceptionDetailInfo
                {
                    Timestamp = ex.Timestamp,
                    SourceType = ex.SourceType,
                    SourceDescription = ex.SourceDescription,
                    BusinessDescription = ex.BusinessDescription ?? string.Empty,
                    ExceptionType = ex.Exception.GetType().Name,
                    Message = ex.Exception.Message,
                    StackTrace = ex.Exception.StackTrace ?? string.Empty
                }).ToList()
            };

            return await Task.FromResult(Res.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取DataChannel异常信息失败，ID: {Id}", id);
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
            var channels = _manager.FetchAll();
            
            var summary = new ExceptionSummaryInfo
            {
                TotalChannels = channels.Count,
                ChannelsWithExceptions = channels.Count(c => c.Pipe.HasExceptions),
                TotalCurrentExceptions = channels.Sum(c => c.Pipe.ExceptionPool.Count),
                TotalHistoricalExceptions = channels.Sum(c => c.Pipe.ExceptionPool.TotalExceptionCount),
                ChannelSummaries = channels.Select(channel => new ChannelSummaryInfo
                {
                    ChannelId = channel.Id,
                    PipelineId = channel.Pipe.ExceptionPool.PipelineId,
                    CurrentExceptionCount = channel.Pipe.ExceptionPool.Count,
                    TotalExceptionCount = channel.Pipe.ExceptionPool.TotalExceptionCount,
                    MaxPoolSize = channel.Pipe.ExceptionPool.MaxSize,
                    HasExceptions = channel.Pipe.HasExceptions,
                    LatestException = channel.Pipe.ExceptionPool.GetRecentExceptions(1).FirstOrDefault()?.Timestamp
                }).ToList()
            };

            return await Task.FromResult(Res.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取DataChannel异常统计信息失败");
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
            var channel = _manager.Fetch(id);
            if (channel == null)
            {
                return Res.Fail("未找到指定的DataChannel");
            }

            channel.Pipe.ExceptionPool.Clear();
            return await Task.FromResult(Res.Ok("异常信息已清空"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空DataChannel异常信息失败，ID: {Id}", id);
            return Res.Fail($"清空DataChannel异常信息失败: {ex.Message}");
        }
    }
} 