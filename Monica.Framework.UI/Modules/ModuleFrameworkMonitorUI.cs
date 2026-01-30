using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Modules;
using Monica.Framework.Modules;
using Monica.Framework.UI.Pages;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.Framework.UI.Modules;

public static class ModuleFrameworkMonitorUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 FrameworkMonitorUI 模块
        /// </summary>
        public static ModuleFrameworkMonitorUIGuide AddFrameworkMonitorUI(Action<ModuleFrameworkMonitorUIOption>? action = null)
        {
            return new ModuleFrameworkMonitorUIGuide().Register(action);
        }
    }
}

/// <summary>
/// 框架监控UI模块
/// </summary>
public class ModuleFrameworkMonitorUI(ModuleFrameworkMonitorUIOption option)
    : MoModuleWithDependencies<ModuleFrameworkMonitorUI, ModuleFrameworkMonitorUIOption, ModuleFrameworkMonitorUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.FrameworkMonitorUI;
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
                    "监控",
                    addToNav: true,
                    navOrder: 20));
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