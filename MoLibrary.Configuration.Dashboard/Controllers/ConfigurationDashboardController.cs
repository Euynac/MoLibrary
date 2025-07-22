using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Controllers;

/// <summary>
/// 配置仪表板控制器，提供配置中心管理API
/// </summary>
[ApiController]
[Route("api/configuration")]
public class ConfigurationDashboardController : MoModuleControllerBase
{
    private readonly IMoConfigurationCentre _configCentre;
    private readonly IMoConfigurationDashboard _dashboard;
    private readonly IMoConfigurationStores _stores;
    private readonly IMoUnitOfWorkManager _uowManager;
    private readonly ILogger<ConfigurationDashboardController> _logger;

    public ConfigurationDashboardController(
        IMoConfigurationCentre configCentre,
        IMoConfigurationDashboard dashboard,
        IMoConfigurationStores stores,
        IMoUnitOfWorkManager uowManager,
        ILogger<ConfigurationDashboardController> logger)
    {
        _configCentre = configCentre;
        _dashboard = dashboard;
        _stores = stores;
        _uowManager = uowManager;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有微服务配置状态
    /// </summary>
    /// <param name="mode">显示模式</param>
    /// <returns>配置状态列表</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetAllConfigStatus([FromQuery] string? mode = null)
    {
        try
        {
            if ((await _configCentre.GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
                return BadRequest(error);

            if ((await _dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
                return BadRequest(error);

            return Ok(Res.Ok(arranged));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有配置状态失败");
            return BadRequest(Res.Fail($"获取所有配置状态失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    /// <param name="appid">应用ID</param>
    /// <param name="key">配置键</param>
    /// <returns>配置状态</returns>
    [HttpGet("option/status")]
    public async Task<IActionResult> GetOptionItemStatus([FromQuery] string? appid, [FromQuery] string key)
    {
        try
        {
            if ((await _configCentre.GetSpecificOptionItemAsync(key, appid)).IsFailed(out var error, out var data))
                return BadRequest(error);

            return Ok(Res.Ok(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取指定配置状态失败");
            return BadRequest(Res.Fail($"获取指定配置状态失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取配置类历史
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>配置历史</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetConfigHistory(
        [FromQuery] string? key, 
        [FromQuery] string? appid,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end)
    {
        try
        {
            using var uow = _uowManager.Begin();

            if (appid != null && key != null)
            {
                var result = await _stores.GetHistory(key, appid);
                return Ok(result);
            }

            if (start != null && end != null)
            {
                var result = await _stores.GetHistory(start.Value, end.Value);
                return Ok(result);
            }

            var defaultResult = await _stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now);
            return Ok(defaultResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置历史失败");
            return BadRequest(Res.Fail($"获取配置历史失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateConfig([FromBody] DtoUpdateConfig request)
    {
        try
        {
            using var uow = _uowManager.Begin();
            var result = await _configCentre.UpdateConfig(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败");
            return BadRequest(Res.Fail($"更新配置失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 回滚配置类
    /// </summary>
    /// <param name="request">回滚请求</param>
    /// <returns>回滚结果</returns>
    [HttpPost("rollback")]
    public async Task<IActionResult> RollbackConfig([FromBody] RollbackRequest request)
    {
        try
        {
            using var uow = _uowManager.Begin();
            var result = await _configCentre.RollbackConfig(request.Key, request.AppId, request.Version);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚配置失败");
            return BadRequest(Res.Fail($"回滚配置失败: {ex.Message}"));
        }
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