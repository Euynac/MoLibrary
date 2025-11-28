using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.JobScheduler.UI.Pages;
using MoLibrary.JobScheduler.UI.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.JobScheduler.UI.Modules;

/// <summary>
/// JobScheduler UI 模块实现
/// 提供基于 Blazor 的作业调度管理界面
/// </summary>
public class ModuleJobSchedulerUI(ModuleJobSchedulerUIOption option)
    : MoModuleWithDependencies<ModuleJobSchedulerUI, ModuleJobSchedulerUIOption, ModuleJobSchedulerUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.JobSchedulerUI;
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

        // 注册 Scoped 门面服务（Blazor Circuit）
        services.AddScoped<JobSchedulerUIService>();
    }

    public override void ClaimDependencies()
    {
        // 依赖后端 JobScheduler 模块
        DependsOnModule<ModuleJobSchedulerGuide>().Register();

        // 依赖 UI 核心模块并注册页面
        if (!Option.DisableJobSchedulerPages)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p =>
                {
                    p.RegisterComponent<JobDefinitionsPage>(
                        JobDefinitionsPage.PAGE_URL,
                        "任务定义",
                        Icons.Material.Filled.WorkOutline,
                        "任务调度",
                        addToNav: true,
                        navOrder: 100);
        
                    p.RegisterComponent<JobInstancesPage>(
                        JobInstancesPage.PAGE_URL,
                        "任务实例",
                        Icons.Material.Filled.PlaylistPlay,
                        "任务调度",
                        addToNav: true,
                        navOrder: 101);
                });
        }
    }
}
