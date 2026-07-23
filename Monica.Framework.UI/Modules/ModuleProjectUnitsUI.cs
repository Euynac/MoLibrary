using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProjectUnitsUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the project-units UI module.
        /// </summary>
        public ModuleProjectUnitsUIGuide AddProjectUnitsUI(Action<ModuleProjectUnitsUIOption>? action = null)
        {
            return builder.AddModule<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption, ModuleProjectUnitsUIGuide>(action);
        }
    }
}

/// <summary>
    /// Project-units UI module.
/// </summary>
[ModuleKey(BuiltInModuleKey.ProjectUnitsUI)]
public class ModuleProjectUnitsUI(ModuleProjectUnitsUIOption option)
    : ModuleBase<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption, ModuleProjectUnitsUIGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // UI components inject the infrastructure facade directly.
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisablePage)
        {
            DependsOnModule<ModuleLocalizationGuide>().Register()
                .AddResource<ProjectUnitsResource>();

            DependsOnModule<ModuleProjectUnitsGuide>().Register();
            DependsOnModule<ModuleShellUIGuide>().Register(o => o.EnableMarkdown = true)
                .RegisterUIComponents(p => p.RegisterLocalizedPage<UIProjectUnitsPage, ProjectUnitsResource>(
                    UIProjectUnitsPage.PAGE_URL,
                    "Pages:ProjectUnits:Title",
                    Icons.Material.Filled.Monitor,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 20));
        }
    }
}

/// <summary>
/// Project-units UI module guide.
/// </summary>
public class ModuleProjectUnitsUIGuide : ModuleGuide<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption, ModuleProjectUnitsUIGuide>
{
}

/// <summary>
/// Project-units UI module options.
/// </summary>
public class ModuleProjectUnitsUIOption : ModuleOptions<ModuleProjectUnitsUI>
{ 
    /// <summary>
    /// Whether to disable the project-units page.
    /// </summary>
    public bool DisablePage { get; set; }
}
