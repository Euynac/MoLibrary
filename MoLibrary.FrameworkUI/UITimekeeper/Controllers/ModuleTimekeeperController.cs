using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.FrameworkUI.UITimekeeper.Services;

namespace MoLibrary.FrameworkUI.UITimekeeper.Controllers;

/// <summary>
/// Timekeeper相关功能Controller
/// </summary>
/// <param name="timekeeperService">Timekeeper服务</param>
[ApiController]
public class ModuleTimekeeperController(TimekeeperService timekeeperService) : MoModuleControllerBase
{
    /// <summary>
    /// 获取Timekeeper统计状态
    /// </summary>
    /// <returns>Timekeeper统计信息列表</returns>
    [HttpGet("timekeeper/status")]
    public async Task<IActionResult> GetTimekeeperStatus()
    {
        var result = await timekeeperService.GetTimekeeperStatusAsync();
        return result.GetResponse(this);
    }

    /// <summary>
    /// 获取当前正在运行的Timekeeper
    /// </summary>
    /// <returns>正在运行的Timekeeper信息列表</returns>
    [HttpGet("timekeeper/running")]
    public async Task<IActionResult> GetRunningTimekeepers()
    {
        var result = await timekeeperService.GetRunningTimekeepersAsync();
        return result.GetResponse(this);
    }
} 