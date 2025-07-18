using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UITimekeeper.Controllers;
using MoLibrary.FrameworkUI.UITimekeeper.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

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
            
            // 注册Controller依赖
            DependsOnModule<ModuleControllersGuide>().Register()
                .RegisterMoControllers<ModuleTimekeeperController>(Option);
            
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UITimekeeperPage>(
                    UITimekeeperPage.TIMEKEEPER_DEBUG_URL, 
                    "Timekeeper调试", 
                    Icons.Material.Filled.Timer, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 50));
        }
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