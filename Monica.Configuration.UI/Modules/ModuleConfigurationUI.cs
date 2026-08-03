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
using Monica.UI.Shell.Models;
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
                registry.RegisterLocalizedPage<ConfigurationStatePage, ConfigurationUIResource>(
                    ConfigurationUiRoutes.STATE_ROUTE,
                    "Pages:ConfigurationState:Title",
                    Icons.Material.Filled.Tune,
                    BuiltInNavigationCategoryIds.Configuration,
                    addToNav: true,
                    navOrder: 10);

                registry.RegisterLocalizedPage<ConfigurationHistoryPage, ConfigurationUIResource>(
                    ConfigurationUiRoutes.HISTORY_ROUTE,
                    "Pages:ConfigurationHistory:Title",
                    Icons.Material.Filled.History,
                    BuiltInNavigationCategoryIds.Configuration,
                    addToNav: true,
                    navOrder: 20);

                registry.RegisterLocalizedPage<ConfigurationVersionsPage, ConfigurationUIResource>(
                    ConfigurationUiRoutes.VERSIONS_ROUTE,
                    "Pages:ConfigurationVersions:Title",
                    Icons.Material.Filled.SettingsBackupRestore,
                    BuiltInNavigationCategoryIds.Configuration,
                    addToNav: true,
                    navOrder: 25);

                registry.RegisterLocalizedPage<ConfigurationDebugPage, ConfigurationUIResource>(
                    ConfigurationUiRoutes.DEBUG_ROUTE,
                    "Pages:ConfigurationDebug:Title",
                    Icons.Material.Filled.BugReport,
                    BuiltInNavigationCategoryIds.Configuration,
                    addToNav: true,
                    navOrder: 30);

                registry.RegisterLocalizedPage<ConfigurationStoragePage, ConfigurationUIResource>(
                    ConfigurationUiRoutes.STORAGE_ROUTE,
                    "Pages:ConfigurationStorage:Title",
                    Icons.Material.Filled.Storage,
                    BuiltInNavigationCategoryIds.Configuration,
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
        services.AddScoped<ConfigurationVersionsPageState>();
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
    /// <summary>
    /// Gets or sets whether saving a configuration mutation group requires an additional confirmation that lists
    /// the logical services affected by the changed definitions.
    /// </summary>
    /// <remarks>
    /// This option is intended for microservice architectures in which multiple logical services share a Monica
    /// configuration store. It is disabled by default because a single-process or modular-monolith host normally
    /// does not need a cross-service impact prompt. The displayed impact is a point-in-time view of publisher
    /// metadata, not proof of service liveness or reload delivery. If impact analysis is unavailable, the operator
    /// may explicitly choose to save anyway; normal validation and optimistic-concurrency checks still apply.
    /// </remarks>
    public bool EnableAffectedServiceConfirmation { get; set; }
}
