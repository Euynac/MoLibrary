using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
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
        /// 配置 TimekeeperUI 模块
        /// </summary>
        public static ModuleTimekeeperUIGuide AddTimekeeperUI(Action<ModuleTimekeeperUIOption>? action = null)
        {
            return new ModuleTimekeeperUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Timekeeper UI模块
/// </summary>
public class ModuleTimekeeperUI(ModuleTimekeeperUIOption option)
    : MoModule<ModuleTimekeeperUI, ModuleTimekeeperUIOption, ModuleTimekeeperUIGuide>(option)
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