using Microsoft.AspNetCore.Mvc;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Dashboard.UIConfiguration.Services;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Configuration.Dashboard.Controllers;

/// <summary>
/// 配置仪表板控制器，提供配置中心管理API
/// </summary>
[ApiController]
public class ConfigurationDashboardController(ConfigurationDashboardService configurationDashboardService) : MoModuleControllerBase
{

    /// <summary>
    /// 获取所有微服务配置状态
    /// </summary>
    /// <param name="mode">显示模式</param>
    /// <returns>配置状态列表</returns>
    [HttpGet("configuration/status")]
    public async Task<IActionResult> GetAllConfigStatus([FromQuery] string? mode = null)
    {
        var result = await configurationDashboardService.GetAllConfigStatusAsync(mode);
        return result.GetResponse(this);
    }

    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    /// <param name="appid">应用ID</param>
    /// <param name="key">配置键</param>
    /// <returns>配置状态</returns>
    [HttpGet("configuration/status")]
    public async Task<IActionResult> GetOptionItemStatus([FromQuery] string? appid, [FromQuery] string key)
    {
        var result = await configurationDashboardService.GetOptionItemStatusAsync(appid, key);
        return result.GetResponse(this);
    }

    /// <summary>
    /// 获取配置类历史
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>配置历史</returns>
    [HttpGet("configuration/history")]
    public async Task<IActionResult> GetConfigHistory(
        [FromQuery] string? key, 
        [FromQuery] string? appid,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end)
    {
        var result = await configurationDashboardService.GetConfigHistoryAsync(key, appid, start, end);
        return result.GetResponse(this);
    }

    /// <summary>
    /// 更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPost("configuration/update")]
    public async Task<IActionResult> UpdateConfig([FromBody] DtoUpdateConfig request)
    {
        var result = await configurationDashboardService.UpdateConfigAsync(request);
        return result.GetResponse(this);
    }

    /// <summary>
    /// 回滚配置类
    /// </summary>
    /// <param name="request">回滚请求</param>
    /// <returns>回滚结果</returns>
    [HttpPost("configuration/rollback")]
    public async Task<IActionResult> RollbackConfig([FromBody] RollbackRequest request)
    {
        var result = await configurationDashboardService.RollbackConfigAsync(request.Key, request.AppId, request.Version);
        return result.GetResponse(this);
    }
}

/// <summary>
/// 回滚请求模型
/// </summary>
public class RollbackRequest
{
    public required string Key { get; set; }
    public required string AppId { get; set; }
    public required string Version { get; set; }
}