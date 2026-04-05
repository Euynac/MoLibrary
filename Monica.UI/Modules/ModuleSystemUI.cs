using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the module system dashboard UI module.
        /// </summary>
        public static ModuleSystemUIGuide AddModuleSystemUI(Action<ModuleSystemUIOption>? action = null)
        {
            return new ModuleSystemUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Module system dashboard UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.ModuleSystemUI)]
public class ModuleSystemUI(ModuleSystemUIOption option)
    : ModuleBase<ModuleSystemUI, ModuleSystemUIOption, ModuleSystemUIGuide>(option)
{
    /// <summary>
    /// Declares the shell dependency and page registration.
    /// </summary>
    public override void ClaimDependencies()
    {
        if (Option.DisableDashboardPage)
        {
            return;
        }

        DependsOnModule<ModuleSystemGuide>().Register();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<ModuleSystemPage>(
                ModuleSystemPage.MODULE_SYSTEM_DASHBOARD_URL,
                "Pages:ModuleSystemDashboard:Title",
                Icons.Material.Filled.Dashboard,
                "Categories:Module",
                addToNav: true,
                navOrder: 10));
    }
}

/// <summary>
/// Fluent guide for the module system dashboard UI module.
/// </summary>
public class ModuleSystemUIGuide
    : ModuleGuide<ModuleSystemUI, ModuleSystemUIOption, ModuleSystemUIGuide>
{
}

/// <summary>
/// Options for the module system dashboard UI module.
/// </summary>
public class ModuleSystemUIOption : ModuleOptions<ModuleSystemUI>
{
    /// <summary>
    /// Gets or sets whether the dashboard page should be disabled.
    /// </summary>
    public bool DisableDashboardPage { get; set; }
}
