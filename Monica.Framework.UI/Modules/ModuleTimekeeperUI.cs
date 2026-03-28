using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Framework.UI.UITimekeeper.Services;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleTimekeeperUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the TimekeeperUI module
        /// </summary>
        public static ModuleTimekeeperUIGuide AddTimekeeperUI(Action<ModuleTimekeeperUIOption>? action = null)
        {
            return new ModuleTimekeeperUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Timekeeper UI module
/// </summary>
[ModuleKey(EMoModuleKey.TimekeeperUI)]
public class ModuleTimekeeperUI(ModuleTimekeeperUIOption option)
    : MoModule<ModuleTimekeeperUI, ModuleTimekeeperUIOption, ModuleTimekeeperUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TimekeeperService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUITimekeeperPage)
        {
            // Register the original Timekeeper module dependency
            DependsOnModule<ModuleTimekeeperGuide>().Register();

            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UITimekeeperPage>(
                    UITimekeeperPage.PAGE_URL,
                    "Pages:TimekeeperDebug:Title",
                    Icons.Material.Filled.Timer,
                    "Categories:Debug",
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
/// TimekeeperUI module wizard
/// </summary>
public class ModuleTimekeeperUIGuide : MoModuleGuide<ModuleTimekeeperUI, ModuleTimekeeperUIOption, ModuleTimekeeperUIGuide>
{
}

/// <summary>
/// TimekeeperUI module options
/// </summary>
public class ModuleTimekeeperUIOption : MoModuleOptionWithMinimalApi<ModuleTimekeeperUI>
{ 
    /// <summary>
    /// Whether to disable the Timekeeper debugging page
    /// </summary>
    public bool DisableUITimekeeperPage { get; set; }
} 
