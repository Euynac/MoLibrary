using Microsoft.AspNetCore.Mvc;
using MoLibrary.Configuration.Services;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Configuration.Controllers;

/// <summary>
/// 配置模块控制器，提供基础配置管理API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ModuleConfigurationController(ModuleConfigurationService moduleConfigurationService) : MoModuleControllerBase
{

    /// <summary>
    /// 获取热配置状态信息
    /// </summary>
    /// <param name="onlyCurDomain">是否只获取当前域配置</param>
    /// <returns>配置状态信息</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetConfigStatus([FromQuery] bool? onlyCurDomain = null)
    {
        var result = await moduleConfigurationService.GetConfigStatusAsync(onlyCurDomain);
        return result.GetResponse(this);
    }

    /// <summary>
    /// 获取配置调试视图
    /// </summary>
    /// <returns>配置调试信息</returns>
    [HttpGet("debug")]
    public async Task<IActionResult> GetDebugView()
    {
        var result = await moduleConfigurationService.GetDebugViewAsync();
        return result.GetResponse(this);
    }

    /// <summary>
    /// 获取配置提供者信息
    /// </summary>
    /// <returns>配置提供者列表</returns>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        var result = await moduleConfigurationService.GetProvidersAsync();
        return result.GetResponse(this);
    }
}