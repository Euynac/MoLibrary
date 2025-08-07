using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Framework.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

/// <summary>
/// FrameworkMonitorUI模块构建器扩展
/// </summary>
public static class ModuleFrameworkMonitorUIBuilderExtensions
{
    public static ModuleFrameworkMonitorUIGuide ConfigModuleFrameworkMonitorUI(this WebApplicationBuilder builder,
        Action<ModuleFrameworkMonitorUIOption>? action = null)
    {
        return new ModuleFrameworkMonitorUIGuide().Register(action);
    }
}

/// <summary>
/// 框架监控UI模块
/// </summary>
public class ModuleFrameworkMonitorUI(ModuleFrameworkMonitorUIOption option)
    : MoModuleWithDependencies<ModuleFrameworkMonitorUI, ModuleFrameworkMonitorUIOption, ModuleFrameworkMonitorUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.FrameworkMonitorUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // UI模块直接使用IFrameworkMonitorService，无需额外注册服务
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUIFrameworkMonitorPage)
        {
            DependsOnModule<ModuleFrameworkMonitorGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIFrameworkMonitorPage>(
                    UIFrameworkMonitorPage.FRAMEWORK_MONITOR_DEBUG_URL, 
                    "框架监控", 
                    Icons.Material.Filled.Monitor, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 200));
        }
    }
}

/// <summary>
/// FrameworkMonitorUI模块向导
/// </summary>
public class ModuleFrameworkMonitorUIGuide : MoModuleGuide<ModuleFrameworkMonitorUI, ModuleFrameworkMonitorUIOption, ModuleFrameworkMonitorUIGuide>
{
}

/// <summary>
/// FrameworkMonitorUI模块选项
/// </summary>
public class ModuleFrameworkMonitorUIOption : MoModuleOption<ModuleFrameworkMonitorUI>
{ 
    /// <summary>
    /// 是否禁用框架监控页面
    /// </summary>
    public bool DisableUIFrameworkMonitorPage { get; set; }
}