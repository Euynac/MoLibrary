using Microsoft.AspNetCore.Mvc;
using MoLibrary.Core.Module.Dashboard.Interfaces;
using MoLibrary.Core.Module.Dashboard.Models;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard;

/// <summary>
/// 模块状态控制器，提供模块系统状态查询的Web API接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ModuleStatusController : ControllerBase
{
    private readonly IModuleSystemStatusService _statusService;

    /// <summary>
    /// 初始化模块状态控制器
    /// </summary>
    /// <param name="statusService">模块系统状态服务</param>
    public ModuleStatusController(IModuleSystemStatusService statusService)
    {
        _statusService = statusService;
    }

    /// <summary>
    /// 获取模块系统整体状态信息
    /// </summary>
    /// <returns>模块系统状态</returns>
    [HttpGet("system-status")]
    public ActionResult<ModuleSystemStatus> GetSystemStatus()
    {
        var status = _statusService.GetSystemStatus();
        return Ok(status);
    }

    /// <summary>
    /// 获取模块系统性能信息
    /// </summary>
    /// <returns>模块系统性能数据</returns>
    [HttpGet("performance")]
    public ActionResult<ModuleSystemPerformance> GetPerformance()
    {
        var performance = _statusService.GetSystemPerformance();
        return Ok(performance);
    }

    /// <summary>
    /// 获取模块注册信息
    /// </summary>
    /// <returns>模块注册信息</returns>
    [HttpGet("registration-info")]
    public ActionResult<ModuleRegistrationInfo> GetRegistrationInfo()
    {
        var info = _statusService.GetRegistrationInfo();
        return Ok(info);
    }

    /// <summary>
    /// 获取指定模块的详细信息
    /// </summary>
    /// <param name="moduleEnum">模块枚举值</param>
    /// <returns>模块详细信息</returns>
    [HttpGet("module/{moduleEnum}")]
    public ActionResult<ModuleDetailInfo> GetModuleDetail(EMoModules moduleEnum)
    {
        var detail = _statusService.GetModuleDetail(moduleEnum);
        if (detail == null)
        {
            return NotFound($"Module {moduleEnum} not found");
        }
        return Ok(detail);
    }

    /// <summary>
    /// 获取所有启用模块的基本信息
    /// </summary>
    /// <returns>启用模块列表</returns>
    [HttpGet("enabled-modules")]
    public ActionResult<List<ModuleBasicInfo>> GetEnabledModules()
    {
        var registrationInfo = _statusService.GetRegistrationInfo();
        return Ok(registrationInfo.EnabledModules);
    }

    /// <summary>
    /// 获取所有禁用模块的基本信息
    /// </summary>
    /// <returns>禁用模块列表</returns>
    [HttpGet("disabled-modules")]
    public ActionResult<List<ModuleBasicInfo>> GetDisabledModules()
    {
        var registrationInfo = _statusService.GetRegistrationInfo();
        return Ok(registrationInfo.DisabledModules);
    }

    /// <summary>
    /// 获取模块依赖关系图
    /// </summary>
    /// <returns>模块依赖关系图</returns>
    [HttpGet("dependency-graph")]
    public ActionResult<ModuleDependencyGraph> GetDependencyGraph()
    {
        var graph = _statusService.GetDependencyGraph();
        return Ok(graph);
    }

    /// <summary>
    /// 获取模块系统健康检查结果
    /// </summary>
    /// <returns>健康检查结果</returns>
    [HttpGet("health-check")]
    public ActionResult<ModuleSystemHealthCheck> GetHealthCheck()
    {
        var health = _statusService.GetHealthCheck();
        return Ok(health);
    }

    /// <summary>
    /// 获取最慢的模块列表
    /// </summary>
    /// <param name="count">返回的模块数量，默认为5</param>
    /// <returns>最慢的模块列表</returns>
    [HttpGet("slowest-modules")]
    public ActionResult<List<ModulePerformanceInfo>> GetSlowestModules([FromQuery] int count = 5)
    {
        var performance = _statusService.GetSystemPerformance();
        var slowestModules = performance.SlowestModules.Take(count).ToList();
        return Ok(slowestModules);
    }

    /// <summary>
    /// 获取系统概览信息（用于仪表板）
    /// </summary>
    /// <returns>系统概览信息</returns>
    [HttpGet("overview")]
    public ActionResult<ModuleSystemOverview> GetOverview()
    {
        var status = _statusService.GetSystemStatus();
        var performance = _statusService.GetSystemPerformance();
        var health = _statusService.GetHealthCheck();

        var overview = new ModuleSystemOverview
        {
            SystemStatus = status,
            TotalInitializationTime = performance.TotalSystemInitializationTimeMs,
            SlowestModules = performance.SlowestModules.Take(3).ToList(),
            OverallHealth = health.OverallHealth,
            CriticalIssuesCount = health.Issues.Count(i => i.Severity >= IssueSeverity.High),
            WarningIssuesCount = health.Issues.Count(i => i.Severity == IssueSeverity.Medium),
            Recommendations = health.Recommendations.Take(3).ToList()
        };

        return Ok(overview);
    }
}

/// <summary>
/// 模块系统概览信息，用于仪表板显示
/// </summary>
public class ModuleSystemOverview
{
    /// <summary>
    /// 系统状态
    /// </summary>
    public ModuleSystemStatus SystemStatus { get; set; } = new();

    /// <summary>
    /// 总初始化时间（毫秒）
    /// </summary>
    public long TotalInitializationTime { get; set; }

    /// <summary>
    /// 最慢的几个模块
    /// </summary>
    public List<ModulePerformanceInfo> SlowestModules { get; set; } = [];

    /// <summary>
    /// 整体健康状态
    /// </summary>
    public HealthStatus OverallHealth { get; set; }

    /// <summary>
    /// 严重问题数量
    /// </summary>
    public int CriticalIssuesCount { get; set; }

    /// <summary>
    /// 警告问题数量
    /// </summary>
    public int WarningIssuesCount { get; set; }

    /// <summary>
    /// 推荐操作
    /// </summary>
    public List<string> Recommendations { get; set; } = [];
} 