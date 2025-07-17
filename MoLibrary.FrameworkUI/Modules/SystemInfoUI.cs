using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
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
public class ModuleSystemInfoUIOption : MoModuleOption<ModuleSystemInfoUI>
{ 
    /// <summary>
    /// 是否禁用系统信息页面
    /// </summary>
    public bool DisableUISystemInfoPage { get; set; }
} 