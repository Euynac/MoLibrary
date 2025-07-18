using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.DataChannel.Dashboard.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Dashboard.Controllers;

[ApiController]
public class ModuleDataChannelController(IDataChannelManager manager) : MoModuleControllerBase
{
    /// <summary>
    /// 对给定ID的DataChannel进行重新初始化操作
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("channel/{id}/re-init")]
    public async Task<IActionResult> ReInit(string id, CancellationToken cancellationToken = default)
    {
        var channel = manager.Fetch(id);

        if (channel is not null)
        {
            return await channel.ReInitialize(cancellationToken).GetResponse(this);
        }

        return NotFound(Res.Fail("未找到对应的DataChannel"));
    }

    /// <summary>
    /// 获取DataChannel状态列表
    /// </summary>
    /// <returns></returns>
    [HttpGet("channels")]
    public IActionResult GetChannels()
    {
        var channels = manager.FetchAll().Select(p => new
        {
            p.Id,
            Middlewares = p.Pipe.GetMiddlewares().Select(m => new ComponentInfo(m)),
            InnerEndpoint = new ComponentInfo(p.Pipe.InnerEndpoint),
            OuterEndpoint = new ComponentInfo(p.Pipe.OuterEndpoint),
            p.Pipe.IsNotAvailable,
            p.Pipe.IsInitialized,
            p.Pipe.IsInitializing,
            p.Pipe.HasExceptions,
            ExceptionCount = p.Pipe.ExceptionPool.Count,
            TotalExceptionCount = p.Pipe.ExceptionPool.TotalExceptionCount
        });

        return Ok(channels);
    }

    /// <summary>
    /// 获取指定DataChannel的异常信息
    /// </summary>
    /// <param name="id">DataChannel的ID</param>
    /// <param name="count">获取的异常数量，默认为10</param>
    /// <returns></returns>
    [HttpGet("channel/{id}/exceptions")]
    public IActionResult GetChannelExceptions(string id, int count = 10)
    {
        var channel = manager.Fetch(id);

        if (channel is null)
        {
            return NotFound(Res.Fail("未找到对应的DataChannel"));
        }

        var exceptions = channel.Pipe.ExceptionPool.GetRecentExceptions(count);
        
        var result = exceptions.Select(ex => new
        {
            ex.Timestamp,
            ex.SourceType,
            ex.SourceDescription,
            Exception = new
            {
                Type = ex.Exception.GetType().Name,
                Message = ex.Exception.Message,
                StackTrace = ex.Exception.StackTrace
            }
        });

        return Ok(new
        {
            ChannelId = id,
            PipelineId = channel.Pipe.ExceptionPool.PipelineId,
            CurrentExceptions = channel.Pipe.ExceptionPool.Count,
            TotalExceptions = channel.Pipe.ExceptionPool.TotalExceptionCount,
            MaxPoolSize = channel.Pipe.ExceptionPool.MaxSize,
            HasExceptions = channel.Pipe.HasExceptions,
            Exceptions = result
        });
    }

    /// <summary>
    /// 获取所有DataChannel的异常统计信息
    /// </summary>
    /// <returns></returns>
    [HttpGet("channels/exceptions/summary")]
    public IActionResult GetChannelsExceptionsSummary()
    {
        var channels = manager.FetchAll();
        
        var summary = channels.Select(channel => new
        {
            ChannelId = channel.Id,
            PipelineId = channel.Pipe.ExceptionPool.PipelineId,
            CurrentExceptionCount = channel.Pipe.ExceptionPool.Count,
            TotalExceptionCount = channel.Pipe.ExceptionPool.TotalExceptionCount,
            MaxPoolSize = channel.Pipe.ExceptionPool.MaxSize,
            HasExceptions = channel.Pipe.HasExceptions,
            LatestException = channel.Pipe.ExceptionPool.GetRecentExceptions(1).FirstOrDefault()?.Timestamp
        });

        return Ok(new
        {
            TotalChannels = channels.Count,
            ChannelsWithExceptions = channels.Count(c => c.Pipe.HasExceptions),
            TotalCurrentExceptions = channels.Sum(c => c.Pipe.ExceptionPool.Count),
            TotalHistoricalExceptions = channels.Sum(c => c.Pipe.ExceptionPool.TotalExceptionCount),
            Summary = summary
        });
    }

    /// <summary>
    /// 清空指定DataChannel的异常信息
    /// </summary>
    /// <param name="id">DataChannel的ID</param>
    /// <returns></returns>
    [HttpDelete("channel/{id}/exceptions")]
    public IActionResult ClearChannelExceptions(string id)
    {
        var channel = manager.Fetch(id);

        if (channel is null)
        {
            return NotFound(Res.Fail("未找到对应的DataChannel"));
        }

        channel.Pipe.ExceptionPool.Clear();
        
        return Ok(Res.Ok("异常信息已清空"));
    }

    
}