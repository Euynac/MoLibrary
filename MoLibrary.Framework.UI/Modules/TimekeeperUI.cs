using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Framework.UI.UITimekeeper.Services;
using MoLibrary.Framework.UI.Pages;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.Framework.UI.Modules;

/// <summary>
/// TimekeeperUI模块构建器扩展
/// </summary>
public static class ModuleTimekeeperUIBuilderExtensions
{
    public static ModuleTimekeeperUIGuide ConfigModuleTimekeeperUI(this WebApplicationBuilder builder,
        Action<ModuleTimekeeperUIOption>? action = null)
    {
        return new ModuleTimekeeperUIGuide().Register(action);
    }
}

/// <summary>
/// Timekeeper UI模块
/// </summary>
public class ModuleTimekeeperUI(ModuleTimekeeperUIOption option)
    : MoModuleWithDependencies<ModuleTimekeeperUI, ModuleTimekeeperUIOption, ModuleTimekeeperUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.TimekeeperUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TimekeeperService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUITimekeeperPage)
        {
            // 注册原有的Timekeeper模块依赖
            DependsOnModule<ModuleTimekeeperGuide>().Register();

            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UITimekeeperPage>(
                    UITimekeeperPage.PAGE_URL,
                    "Timekeeper调试",
                    Icons.Material.Filled.Timer,
                    "系统管理",
                    addToNav: true,
                    navOrder: 50));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "Timekeeper相关接口" } };

            endpoints.MapGet("/timekeeper/status",
                async ([FromServices] TimekeeperService timekeeperService) =>
                {
                    var result = await timekeeperService.GetTimekeeperStatusAsync();
                    return result.GetResponse();
                })
                .WithName("获取Timekeeper统计状态").WithOpenApi(operation =>
                {
                    operation.Summary = "获取Timekeeper统计状态";
                    operation.Description = "获取Timekeeper统计信息列表";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/timekeeper/running",
                async ([FromServices] TimekeeperService timekeeperService) =>
                {
                    var result = await timekeeperService.GetRunningTimekeepersAsync();
                    return result.GetResponse();
                })
                .WithName("获取当前正在运行的Timekeeper").WithOpenApi(operation =>
                {
                    operation.Summary = "获取当前正在运行的Timekeeper";
                    operation.Description = "获取正在运行的Timekeeper信息列表";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }
}

/// <summary>
/// TimekeeperUI模块向导
/// </summary>
public class ModuleTimekeeperUIGuide : MoModuleGuide<ModuleTimekeeperUI, ModuleTimekeeperUIOption, ModuleTimekeeperUIGuide>
{
}

/// <summary>
/// TimekeeperUI模块选项
/// </summary>
public class ModuleTimekeeperUIOption : MoModuleControllerOption<ModuleTimekeeperUI>
{ 
    /// <summary>
    /// 是否禁用Timekeeper调试页面
    /// </summary>
    public bool DisableUITimekeeperPage { get; set; }
} 