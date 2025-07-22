using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Controllers;

/// <summary>
/// 配置模块控制器，提供基础配置管理API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ModuleConfigurationController : MoModuleControllerBase
{
    private readonly IMoConfigurationCardManager _configCardManager;
    private readonly ILogger<ModuleConfigurationController> _logger;

    public ModuleConfigurationController(
        IMoConfigurationCardManager configCardManager,
        ILogger<ModuleConfigurationController> logger)
    {
        _configCardManager = configCardManager;
        _logger = logger;
    }

    /// <summary>
    /// 获取热配置状态信息
    /// </summary>
    /// <param name="onlyCurDomain">是否只获取当前域配置</param>
    /// <returns>配置状态信息</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetConfigStatus([FromQuery] bool? onlyCurDomain = null)
    {
        try
        {
            var result = _configCardManager.GetDomainConfigs(onlyCurDomain);
            return Ok(Res.Create(result, ResponseCode.Ok));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置状态失败");
            return BadRequest(Res.Fail($"获取配置状态失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取配置调试视图
    /// </summary>
    /// <returns>配置调试信息</returns>
    [HttpGet("debug")]
    public async Task<IActionResult> GetDebugView()
    {
        try
        {
            var debugView = MoConfigurationManager.GetDebugView().Split(Environment.NewLine);
            var result = new { debug = debugView };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置调试视图失败");
            return BadRequest(Res.Fail($"获取配置调试视图失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取配置提供者信息
    /// </summary>
    /// <returns>配置提供者列表</returns>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        try
        {
            var providers = MoConfigurationManager.GetProviders();
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置提供者失败");
            return BadRequest(Res.Fail($"获取配置提供者失败: {ex.Message}"));
        }
    }
}