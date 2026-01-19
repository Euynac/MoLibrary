using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.TimekeeperUI;
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
                    "调试",
                    addToNav: true,
                    navOrder: 30));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/timekeeper/status",
                async ([FromServices] TimekeeperService timekeeperService) =>
                {
                    var result = await timekeeperService.GetTimekeeperStatusAsync();
                    return result.GetResponse();
                })
                .WithName("获取Timekeeper统计状态")
                .WithTags(tagName)
                .WithSummary("获取Timekeeper统计状态")
                .WithDescription("获取Timekeeper统计信息列表");

            endpoints.MapGet("/timekeeper/running",
                async ([FromServices] TimekeeperService timekeeperService) =>
                {
                    var result = await timekeeperService.GetRunningTimekeepersAsync();
                    return result.GetResponse();
                })
                .WithName("获取当前正在运行的Timekeeper")
                .WithTags(tagName)
                .WithSummary("获取当前正在运行的Timekeeper")
                .WithDescription("获取正在运行的Timekeeper信息列表");
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
public class ModuleTimekeeperUIOption : MoModuleOptionWithMinimalApi<ModuleTimekeeperUI>
{ 
    /// <summary>
    /// 是否禁用Timekeeper调试页面
    /// </summary>
    public bool DisableUITimekeeperPage { get; set; }
} 