
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;
using MoLibrary.FrameworkUI.UISystemInfo.Services;

namespace MoLibrary.FrameworkUI.UISystemInfo.Controllers;

/// <summary>
/// 系统信息相关功能Controller
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="systemInfoService">系统信息服务</param>
[ApiController]
public class ModuleSystemInfoController(SystemInfoService systemInfoService) : MoModuleControllerBase
{

    /// <summary>
    /// 获取微服务信息
    /// </summary>
    /// <param name="simple">是否简化输出</param>
    /// <returns>系统信息</returns>
    [HttpGet("system/info")]
    public async Task<IActionResult> GetSystemInfo([FromQuery] bool? simple = null)
    {
        // 调用服务层实现
        var result = await systemInfoService.GetSystemInfoAsync(simple);
        return result.GetResponse(this);
    }
} 