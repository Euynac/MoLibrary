using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Pages;
using Monica.Configuration.UI.State;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the configuration operator console UI module.
        /// </summary>
        /// <param name="action">Optional module option configuration.</param>
        /// <returns>The module guide used to continue configuration.</returns>
        public ModuleConfigurationUIGuide AddConfigurationUI(Action<ModuleConfigurationUIOption>? action = null)
        {
            return builder.AddModule<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(action);
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

        DependsOnModule<ModuleDiffHighlightGuide>().Register();

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry =>
            {
                registry.RegisterLocalizedComponent<ConfigurationStatePage>(
                    ConfigurationUiRoutes.STATE_ROUTE,
                    "Pages:ConfigurationState:Title",
                    Icons.Material.Filled.Tune,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 10,
                    navLinkMatch: NavLinkMatch.All);

                registry.RegisterLocalizedComponent<ConfigurationHistoryPage>(
                    ConfigurationUiRoutes.HISTORY_ROUTE,
                    "Pages:ConfigurationHistory:Title",
                    Icons.Material.Filled.History,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 20);

                registry.RegisterLocalizedComponent<ConfigurationVersionsPage>(
                    ConfigurationUiRoutes.VERSIONS_ROUTE,
                    "Pages:ConfigurationVersions:Title",
                    Icons.Material.Filled.SettingsBackupRestore,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 25);

                registry.RegisterLocalizedComponent<ConfigurationDebugPage>(
                    ConfigurationUiRoutes.DEBUG_ROUTE,
                    "Pages:ConfigurationDebug:Title",
                    Icons.Material.Filled.BugReport,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 30);

                registry.RegisterLocalizedComponent<ConfigurationStoragePage>(
                    ConfigurationUiRoutes.STORAGE_ROUTE,
                    "Pages:ConfigurationStorage:Title",
                    Icons.Material.Filled.Storage,
                    "Categories:Configuration",
                    addToNav: true,
                    navOrder: 40);
            });
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ConfigurationStateStore>();
        services.AddScoped<ConfigurationPendingChangeCompactor>();
        services.AddScoped<ConfigurationJsonDraftService>();
        services.AddScoped<ConfigurationParameterPackageService>();
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
