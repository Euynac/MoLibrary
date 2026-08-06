using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the module system dashboard UI module.
        /// </summary>
        public ModuleRegistration<ModuleSystemUI, ModuleSystemUIOption> AddModuleSystemUI(
            Action<ModuleSystemUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleSystemUI, ModuleSystemUIOption>(action);
            registration.Require<ModuleSystem, ModuleSystemOption>();
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<SharedResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<ModuleSystemPage, SharedResource>(
                    ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL,
                    "Pages:ModuleSystemDashboard:Title",
                    Icons.Material.Filled.Dashboard,
                    BuiltInNavigationCategoryIds.Module,
                    addToNav: true,
                    navOrder: 10));
            return registration;
        }
    }
}

/// <summary>
/// Module system dashboard UI module.
/// </summary>
public class ModuleSystemUI : MonicaModule<ModuleSystemUIOption>, IUIModule
{
}

/// <summary>
/// Options for the module system dashboard UI module.
/// </summary>
public class ModuleSystemUIOption : ModuleOptions<ModuleSystemUI>
{
}
