using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using Monica.Framework.UI.UIProjectUnits.Services;
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
        public ModuleRegistration<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption> AddProjectUnitsUI(
            Action<ModuleProjectUnitsUIOption>? action = null)
        {
            return builder.AddModule<ModuleProjectUnitsUI, ModuleProjectUnitsUIOption>(action);
        }
    }
}

/// <summary>
/// Project-units UI module.
/// </summary>
public class ModuleProjectUnitsUI : MonicaModule<ModuleProjectUnitsUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleProjectUnits, ModuleProjectUnitsOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<ProjectUnitsResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIProjectUnitsPage, ProjectUnitsResource>(
                    UIProjectUnitsPage.PAGE_URL,
                    "Pages:ProjectUnits:Title",
                    Icons.Material.Filled.Monitor,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 20)));
    }

    public override void ConfigureServices(ModuleContext<ModuleProjectUnitsUIOption> context)
    {
        context.Services.AddScoped<IProjectUnitsUiDataSource, ProjectUnitsUiDataSource>();
    }
}

/// <summary>
/// Project-units UI module options.
/// </summary>
public class ModuleProjectUnitsUIOption : ModuleOptions<ModuleProjectUnitsUI>
{
}
