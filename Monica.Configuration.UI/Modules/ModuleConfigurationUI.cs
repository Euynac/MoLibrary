using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Pages;
using Monica.Configuration.UI.Support;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Monica.Configuration.UI module.
/// </summary>
public static class ModuleConfigurationUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the configuration operator console UI module.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The module guide used to continue configuration.</returns>
        public static ModuleConfigurationUIGuide AddConfigurationUI(Action<ModuleConfigurationUIOption>? action = null)
        {
            return new ModuleConfigurationUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Operator console UI for Monica.Configuration.
/// </summary>
/// <param name="option">The module options.</param>
[ModuleKey(BuiltInModuleKey.ConfigurationUI)]
public sealed class ModuleConfigurationUI(ModuleConfigurationUIOption option)
    : ModuleBase<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<ConfigurationUIResource>();

        DependsOnModule<ModuleConfigurationGuide>().Register();

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry =>
            {
                registry.RegisterLocalizedComponent<ConfigurationOverviewPage>(
                    ConfigurationUiRoutes.OVERVIEW_ROUTE,
                    "Pages:ConfigurationOverview:Title",
                    Icons.Material.Filled.Settings,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 20,
                    navLinkMatch: NavLinkMatch.All);

                registry.RegisterLocalizedComponent<ConfigurationDefinitionDetailPage>(
                    "/configuration/{definitionKey}",
                    "Pages:ConfigurationDefinitionDetail:Title",
                    Icons.Material.Filled.AccountTree,
                    "Categories:Configuration");

                registry.RegisterLocalizedComponent<ConfigurationValueDetailPage>(
                    "/configuration/{definitionKey}/value",
                    "Pages:ConfigurationValueDetail:Title",
                    Icons.Material.Filled.DataObject,
                    "Categories:Configuration");

                registry.RegisterLocalizedComponent<ConfigurationMutationEditorPage>(
                    "/configuration/{definitionKey}/edit",
                    "Pages:ConfigurationMutationEditor:Title",
                    Icons.Material.Filled.Edit,
                    "Categories:Configuration");

                registry.RegisterLocalizedComponent<ConfigurationHistoryPage>(
                    "/configuration/{definitionKey}/history",
                    "Pages:ConfigurationHistory:Title",
                    Icons.Material.Filled.History,
                    "Categories:Configuration");
            });
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ConfigurationUiState>();
    }
}

/// <summary>
/// Fluent guide for Monica.Configuration.UI.
/// </summary>
public class ModuleConfigurationUIGuide
    : ModuleGuide<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>
{
}

/// <summary>
/// Configuration options for Monica.Configuration.UI.
/// </summary>
public class ModuleConfigurationUIOption : ModuleOptions<ModuleConfigurationUI>
{
}
