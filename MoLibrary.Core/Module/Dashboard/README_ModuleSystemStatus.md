# 模块系统状态 API

本API提供了获取模块系统运行状态、性能指标、依赖关系等信息的结构化接口，用于支持界面展示和系统监控。

## 接口概述

### IModuleSystemStatusService

提供以下主要功能：

- `GetSystemStatus()` - 获取模块系统整体状态
- `GetSystemPerformance()` - 获取性能统计信息
- `GetRegistrationInfo()` - 获取模块注册信息
- `GetModuleDetail()` - 获取单个模块详细信息
- `GetDependencyGraph()` - 获取依赖关系图
- `GetHealthCheck()` - 获取系统健康状态

## 使用示例

### 1. 注册模块

```csharp
// 在应用程序启动时注册模块
builder.RegisterMoLibraryModule<MoModuleSystemStatusModule>();
```

### 2. 获取系统状态

```csharp
public class ModuleStatusController : ControllerBase
{
    private readonly IModuleSystemStatusService _statusService;

    public ModuleStatusController(IModuleSystemStatusService statusService)
    {
        _statusService = statusService;
    }

    [HttpGet("system-status")]
    public ActionResult<ModuleSystemStatus> GetSystemStatus()
    {
        var status = _statusService.GetSystemStatus();
        return Ok(status);
    }

    [HttpGet("performance")]
    public ActionResult<ModuleSystemPerformance> GetPerformance()
    {
        var performance = _statusService.GetSystemPerformance();
        return Ok(performance);
    }

    [HttpGet("registration-info")]
    public ActionResult<ModuleRegistrationInfo> GetRegistrationInfo()
    {
        var info = _statusService.GetRegistrationInfo();
        return Ok(info);
    }

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

    [HttpGet("dependency-graph")]
    public ActionResult<ModuleDependencyGraph> GetDependencyGraph()
    {
        var graph = _statusService.GetDependencyGraph();
        return Ok(graph);
    }

    [HttpGet("health-check")]
    public ActionResult<ModuleSystemHealthCheck> GetHealthCheck()
    {
        var health = _statusService.GetHealthCheck();
        return Ok(health);
    }
}
```

### 3. 创建监控页面

```csharp
public class ModuleMonitoringService
{
    private readonly IModuleSystemStatusService _statusService;

    public ModuleMonitoringService(IModuleSystemStatusService statusService)
    {
        _statusService = statusService;
    }

    public async Task<ModuleDashboardData> GetDashboardDataAsync()
    {
        var systemStatus = _statusService.GetSystemStatus();
        var performance = _statusService.GetSystemPerformance();
        var healthCheck = _statusService.GetHealthCheck();

        return new ModuleDashboardData
        {
            SystemStatus = systemStatus,
            Performance = performance,
            HealthCheck = healthCheck,
            SlowestModules = performance.SlowestModules,
            CriticalIssues = healthCheck.Issues
                .Where(i => i.Severity >= IssueSeverity.High)
                .ToList()
        };
    }

    public void LogSystemStatus()
    {
        var status = _statusService.GetSystemStatus();
        var performance = _statusService.GetSystemPerformance();

        Console.WriteLine($"模块系统状态: {status.State}");
        Console.WriteLine($"总模块数: {status.TotalModules}");
        Console.WriteLine($"启用模块: {status.EnabledModules}");
        Console.WriteLine($"禁用模块: {status.DisabledModules}");
        Console.WriteLine($"初始化时间: {status.TotalInitializationTimeMs}ms");

        if (performance.SlowestModules.Any())
        {
            Console.WriteLine("\n最慢的模块:");
            foreach (var module in performance.SlowestModules.Take(3))
            {
                Console.WriteLine($"  {module.ModuleTypeName}: {module.TotalDurationMs}ms");
            }
        }
    }
}

public class ModuleDashboardData
{
    public ModuleSystemStatus SystemStatus { get; set; }
    public ModuleSystemPerformance Performance { get; set; }
    public ModuleSystemHealthCheck HealthCheck { get; set; }
    public List<ModulePerformanceInfo> SlowestModules { get; set; } = [];
    public List<HealthIssue> CriticalIssues { get; set; } = [];
}
```

## 数据模型说明

### ModuleSystemStatus
- 包含系统整体状态信息
- 模块数量统计
- 初始化时间
- 错误和依赖问题标识

### ModuleSystemPerformance
- 系统和模块性能指标
- 各阶段耗时统计
- 最慢模块排名
- 配置方法统计

### ModuleRegistrationInfo
- 启用和禁用的模块列表
- 模块注册顺序
- 统计信息

### ModuleDetailInfo
- 单个模块的完整信息
- 包含基本信息、性能、依赖、配置等

### ModuleDependencyGraph
- 模块依赖关系图
- 节点和边信息
- 循环依赖检测
- 拓扑排序结果

### ModuleSystemHealthCheck
- 系统健康状态评估
- 问题检测和建议
- 性能评分

## 监控建议

1. **定期健康检查**: 建议每5-10分钟执行一次健康检查
2. **性能监控**: 关注初始化时间超过阈值的模块
3. **依赖关系**: 监控循环依赖的出现
4. **错误跟踪**: 及时处理模块注册错误
5. **趋势分析**: 记录性能数据用于趋势分析

## 注意事项

- 该API主要用于获取静态的模块系统信息
- 性能数据反映的是初始化阶段的耗时
- 健康检查提供实时的系统状态评估
- 建议在生产环境中谨慎使用，避免频繁调用影响性能 