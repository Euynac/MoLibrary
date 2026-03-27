using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleFrameworkMonitorUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the FrameworkMonitorUI module
        /// </summary>
        public static ModuleFrameworkMonitorUIGuide AddFrameworkMonitorUI(Action<ModuleFrameworkMonitorUIOption>? action = null)
        {
            return new ModuleFrameworkMonitorUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Framework monitoring UI module
/// </summary>
[ModuleKey(EMoModuleKey.FrameworkMonitorUI)]
public class ModuleFrameworkMonitorUI(ModuleFrameworkMonitorUIOption option)
    : MoModule<ModuleFrameworkMonitorUI, ModuleFrameworkMonitorUIOption, ModuleFrameworkMonitorUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // The UI module uses IFrameworkMonitorService directly without additional registration of services.
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUIFrameworkMonitorPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<FrameworkMonitorResource>();

            DependsOnModule<ModuleFrameworkMonitorGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIFrameworkMonitorPage>(
                    UIFrameworkMonitorPage.FRAMEWORK_MONITOR_DEBUG_URL,
                    "Pages:FrameworkMonitor:Title",
                    Icons.Material.Filled.Monitor,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 20));
        }
    }
}

/// <summary>
/// FrameworkMonitorUI Module Wizard
/// </summary>
public class ModuleFrameworkMonitorUIGuide : MoModuleGuide<ModuleFrameworkMonitorUI, ModuleFrameworkMonitorUIOption, ModuleFrameworkMonitorUIGuide>
{
}

/// <summary>
/// FrameworkMonitorUI module options
/// </summary>
public class ModuleFrameworkMonitorUIOption : MoModuleOption<ModuleFrameworkMonitorUI>
{ 
    /// <summary>
    /// Whether to disable the frame monitoring page
    /// </summary>
    public bool DisableUIFrameworkMonitorPage { get; set; }
}
