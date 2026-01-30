using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.JobScheduler.Modules;
using Monica.JobScheduler.UI.Pages;
using Monica.JobScheduler.UI.Services;
using Monica.UI.Modules;
using Monica.UI.UIStackTrace.Services;
using MudBlazor;

namespace Monica.JobScheduler.UI.Modules;

public static class ModuleJobSchedulerUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 JobSchedulerUI 模块
        /// </summary>
        public static ModuleJobSchedulerUIGuide AddJobSchedulerUI(Action<ModuleJobSchedulerUIOption>? action = null)
        {
            return new ModuleJobSchedulerUIGuide().Register(action);
        }
    }
}

/// <summary>
/// JobScheduler UI 模块实现
/// 提供基于 Blazor 的作业调度管理界面
/// </summary>
public class ModuleJobSchedulerUI(ModuleJobSchedulerUIOption option)
    : MoModuleWithDependencies<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.JobSchedulerUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册 Singleton 服务（性能优化）
        services.AddSingleton<JobDefinitionQueryService>();
        services.AddSingleton<JobInstanceQueryService>();
        services.AddSingleton<JobStatisticsService>();
        services.AddSingleton<JobHealthMetricsService>();
        services.AddSingleton<JobStateColorService>();
        services.AddSingleton<CronExpressionService>();
        services.AddSingleton<JobArgsSchemaService>();

        // 注册仪表盘、监控、分析服务
        services.AddSingleton<DashboardDataLoader>();
        services.AddSingleton<JobDashboardService>();
        services.AddSingleton<JobMonitorService>();
        services.AddSingleton<JobAnalyticsService>();

        // 注册 Scoped 门面服务（Blazor Circuit）
        services.AddScoped<JobSchedulerUIService>();
        // StackTraceParserService is now registered by ModuleUIStackTrace module
    }

    public override void ClaimDependencies()
    {
        // 依赖后端 JobScheduler 模块
        DependsOnModule<ModuleJobSchedulerGuide>().Register();

        // 依赖 UIStackTrace 模块（用于堆栈跟踪可视化）
        DependsOnModule<ModuleUIStackTraceGuide>().Register();

        // 依赖 UI 核心模块并注册页面
        if (!Option.DisableJobSchedulerPages)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    // 总览仪表盘
                    p.RegisterComponent<DashboardPage>(
                        DashboardPage.PAGE_URL,
                        "总览",
                        Icons.Material.Filled.Dashboard,
                        "任务调度",
                        addToNav: true,
                        navOrder: 99,
                        navLinkMatch: NavLinkMatch.All);

                    // 实时监控
                    p.RegisterComponent<MonitorPage>(
                        MonitorPage.PAGE_URL,
                        "实时监控",
                        Icons.Material.Filled.Monitor,
                        "任务调度",
                        addToNav: true,
                        navOrder: 100);

                    p.RegisterComponent<JobDefinitionsPage>(
                        JobDefinitionsPage.PAGE_URL,
                        "任务定义",
                        Icons.Material.Filled.WorkOutline,
                        "任务调度",
                        addToNav: true,
                        navOrder: 101);

                    p.RegisterComponent<JobInstancesPage>(
                        JobInstancesPage.PAGE_URL,
                        "任务实例",
                        Icons.Material.Filled.PlaylistPlay,
                        "任务调度",
                        addToNav: true,
                        navOrder: 102);

                    // 统计分析
                    p.RegisterComponent<StatisticsPage>(
                        StatisticsPage.PAGE_URL,
                        "统计分析",
                        Icons.Material.Filled.Analytics,
                        "任务调度",
                        addToNav: true,
                        navOrder: 103);
                });
        }
    }
}

/// <summary>
/// JobScheduler UI 模块配置指南
/// </summary>
public class ModuleJobSchedulerUIGuide
    : MoModuleGuide<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>
{
    // 配置方法可在后续需要时添加
    // 目前通过 Mo.AddJobSchedulerUI(options => { ... }) 直接配置即可
}

/// <summary>
/// JobScheduler UI 模块配置选项
/// </summary>
public class ModuleJobSchedulerUIOption : MoModuleOption<ModuleJobSchedulerUI>
{
    /// <summary>
    /// 禁用 JobScheduler UI 页面
    /// </summary>
    public bool DisableJobSchedulerPages { get; set; } = false;

    /// <summary>
    /// 健康指标时间窗口（默认 30 天）
    /// 配置统计近 x 时间健康度
    /// </summary>
    public TimeSpan HealthMetricsWindow { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// 健康指标中显示的最近失败实例数量（默认 5）
    /// 配置显示最近 x 个失败实例记录
    /// </summary>
    public int HealthMetricsFailedInstancesLimit { get; set; } = 5;

    /// <summary>
    /// 表格默认分页大小（默认 20）
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// 自动刷新间隔（默认 5 秒）
    /// 设置为更高值（如 10 秒）以减少服务器负载
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 默认启用自动刷新
    /// </summary>
    public bool EnableAutoRefreshByDefault { get; set; } = true;
}
