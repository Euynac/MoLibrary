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

public static class ModuleProjectUnitsUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the project-units UI module.
        /// </summary>
        public static ModuleProjectUnitsUIGuide AddProjectUnitsUI(Action<ModuleProjectUnitsUIOption>? action = null)
        {
            return new ModuleProjectUnitsUIGuide().Register(action);
        }
    }
}

/// <summary>
    /// Project-units UI module.
/// </summary>
[ModuleKey(EMoModuleKey.ProjectUnitsUI)]
public class ModuleProjectUnitsUI(ModuleProjectUnitsUIOption option)
    : MoModule<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption, ModuleProjectUnitsUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // UI components inject the infrastructure facade directly.
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableProjectUnitsPage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<ProjectUnitsResource>();

            DependsOnModule<ModuleProjectUnitsGuide>().Register();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<UIProjectUnitsPage>(
                    UIProjectUnitsPage.PROJECT_UNITS_PAGE_URL,
                    "Pages:ProjectUnits:Title",
                    Icons.Material.Filled.Monitor,
                    "Categories:Monitor",
                    addToNav: true,
                    navOrder: 20));
        }
    }
}

/// <summary>
/// Project-units UI module guide.
/// </summary>
public class ModuleProjectUnitsUIGuide : MoModuleGuide<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption, ModuleProjectUnitsUIGuide>
{
}

/// <summary>
/// Project-units UI module options.
/// </summary>
public class ModuleProjectUnitsUIOption : MoModuleOption<ModuleProjectUnitsUI>
{ 
    /// <summary>
    /// Whether to disable the project-units page.
    /// </summary>
    public bool DisableProjectUnitsPage { get; set; }
}
