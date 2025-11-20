using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UISystemInfo.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// SystemInfoUI模块构建器扩展
/// </summary>
public static class ModuleSystemInfoUIBuilderExtensions
{
    public static ModuleSystemInfoUIGuide ConfigModuleSystemInfoUI(this WebApplicationBuilder builder,
        Action<ModuleSystemInfoUIOption>? action = null)
    {
        return new ModuleSystemInfoUIGuide().Register(action);
    }
}

/// <summary>
/// 系统信息UI模块
/// </summary>
public class ModuleSystemInfoUI(ModuleSystemInfoUIOption option)
    : MoModuleWithDependencies<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SystemInfoUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SystemInfoService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUISystemInfoPage)
        {
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UISystemInfoPage>(
                    UISystemInfoPage.SYSTEM_INFO_URL,
                    "系统信息",
                    Icons.Material.Filled.Info,
                    "系统管理",
                    addToNav: true,
                    navOrder: 10));
        }
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = Option.GetApiGroupName(), Description = "系统信息相关接口" } };

            endpoints.MapGet("/system/info",
                async ([FromQuery] bool? simple,
                      [FromServices] SystemInfoService systemInfoService) =>
                {
                    var result = await systemInfoService.GetSystemInfoAsync(simple);
                    return result.GetResponse();
                })
                .WithName("获取微服务信息").WithOpenApi(operation =>
                {
                    operation.Summary = "获取微服务信息";
                    operation.Description = "获取微服务信息";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
    }
}

/// <summary>
/// SystemInfoUI模块向导
/// </summary>
public class ModuleSystemInfoUIGuide : MoModuleGuide<ModuleSystemInfoUI, ModuleSystemInfoUIOption, ModuleSystemInfoUIGuide>
{
}

/// <summary>
/// SystemInfoUI模块选项
/// </summary>
public class ModuleSystemInfoUIOption : MoModuleControllerOption<ModuleSystemInfoUI>
{ 
    /// <summary>
    /// 是否禁用系统信息页面
    /// </summary>
    public bool DisableUISystemInfoPage { get; set; }
} 