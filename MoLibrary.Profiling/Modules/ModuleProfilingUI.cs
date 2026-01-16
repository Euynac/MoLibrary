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
                    registry.RegisterComponent<UIProfilingDashboardPage>(
                        UIProfilingDashboardPage.PAGE_URL,
                        "内存分析",
                        Icons.Material.Filled.Memory,
                        "系统管理",
                        addToNav: true,
                        navOrder: 103);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册内存指标收集器为单例 (维护历史数据)
        services.AddSingleton<MemoryMetricsCollector>();

        // 注册内存分析服务为 Scoped
        services.AddScoped<IMemoryAnalysisService, MemoryAnalysisService>();
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
}