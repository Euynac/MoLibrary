using BuildingBlocksPlatform.Extensions;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Dashboard.Controllers;

[Route("data-channel")]
[ApiController]
//[Tags("DataChannel")]
public class DataChannelController(IDataChannelManager manager) : ControllerBase
{
    /// <summary>
    /// 对给定ID的DataChannel进行重新初始化操作
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("channel/{id}/re-init")]
    public async Task<IActionResult> ReInit(string id)
    {
        var channel = manager.Fetch(id);

        if (channel is not null)
        {
            return await channel.ReInitialize().GetResponse(this);
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
            Middlewares = p.Pipe.GetMiddlewares().Select(m => new { m.GetType().Name, metadata = m.GetMetadata() }),
            Endpoints = p.Pipe.GetEndpoints().Select(m => new { m.GetType().Name, metadata = m.GetMetadata() })
        });

        return Ok(channels);
    }
}