using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Profiling.Pages;
using MoLibrary.Profiling.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.Profiling.Modules;

/// <summary>
///     Profiling UI 模块扩展方法
/// </summary>
public static class ModuleProfilingUIBuilderExtensions
{
    /// <summary>
    ///     配置 Profiling UI 模块
    /// </summary>
    public static ModuleProfilingUIGuide ConfigModuleProfilingUI(
        this WebApplicationBuilder builder,
        Action<ModuleProfilingUIOption>? action = null)
    {
        return new ModuleProfilingUIGuide().Register(action);
    }
}

/// <summary>
///     Profiling UI 模块 - 提供内存分析和性能监控界面
/// </summary>
public class ModuleProfilingUI(ModuleProfilingUIOption option)
    : MoModuleWithDependencies<ModuleProfilingUI, ModuleProfilingUIOption, ModuleProfilingUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ProfilingUI;
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableProfilingPage)
        {
            // 依赖 Profiling 模块
            DependsOnModule<ModuleProfilingGuide>().Register();

            // 依赖 UI 核心模块并注册 UI 组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterComponent<UIQuickMonitorPage>(
                        UIQuickMonitorPage.PAGE_URL,
                        "快速监控",
                        Icons.Material.Filled.Speed,
                        "监控",
                        addToNav: true,
                        navOrder: 10);

                    registry.RegisterComponent<UIProfilingDashboardPage>(
                        UIProfilingDashboardPage.PAGE_URL,
                        "内存分析",
                        Icons.Material.Filled.Memory,
                        "监控",
                        addToNav: true,
                        navOrder: 60);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // ProfilingMetricsCollector 已在 ModuleProfiling 中注册

        // 注册内存分析服务为 Scoped
        services.AddScoped<IMemoryAnalysisService, MemoryAnalysisService>();

        // 注册类型分配跟踪服务 (如果启用)
        if (Option.EnableTypeAllocationTracking)
        {
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TypeAllocationCollector>>();
                return new TypeAllocationCollector(
                    logger,
                    maxTrackedTypes: Option.MaxTrackedTypes,
                    autoStopAfter: Option.AutoStopAfter);
            });
            services.AddScoped<ITypeAllocationService, TypeAllocationService>();

            // 注册自动启动服务 (如果启用)
            if (Option.AutoStartCollection)
            {
                services.AddHostedService<TypeAllocationAutoStartService>();
            }
        }
    }
}

/// <summary>
///     Profiling UI 模块配置指南
/// </summary>
public class ModuleProfilingUIGuide
    : MoModuleGuide<ModuleProfilingUI, ModuleProfilingUIOption, ModuleProfilingUIGuide>
{
}

/// <summary>
///     Profiling UI 模块选项
/// </summary>
public class ModuleProfilingUIOption : MoModuleOption<ModuleProfilingUI>
{
    /// <summary>
    ///     禁用 Profiling 管理页面
    /// </summary>
    public bool DisableProfilingPage { get; set; } = false;

    /// <summary>
    ///     自动刷新间隔 (毫秒)，0 表示禁用自动刷新
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;

    /// <summary>
    ///     最大历史数据点数
    /// </summary>
    public int MaxHistoryPoints { get; set; } = 300;

    /// <summary>
    ///     允许手动触发 GC
    /// </summary>
    public bool AllowManualGC { get; set; } = true;

    /// <summary>
    ///     允许生成 GC Dump
    /// </summary>
    public bool AllowGcDump { get; set; } = true;

    /// <summary>
    ///     启用类型分配跟踪功能
    /// </summary>
    public bool EnableTypeAllocationTracking { get; set; } = true;

    /// <summary>
    ///     应用启动时自动开始收集分配事件
    /// </summary>
    public bool AutoStartCollection { get; set; } = false;

    /// <summary>
    ///     默认采样模式 (用于自动启动时)
    /// </summary>
    public Models.AllocationSamplingMode DefaultSamplingMode { get; set; } = Models.AllocationSamplingMode.High;

    /// <summary>
    ///     最大跟踪类型数量
    /// </summary>
    public int MaxTrackedTypes { get; set; } = 500;

    /// <summary>
    ///     自动停止收集时间，null 表示不自动停止
    /// </summary>
    public TimeSpan? AutoStopAfter { get; set; } = TimeSpan.FromMinutes(10);
}