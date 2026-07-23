using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.OpenTelemetry.UI.Localization;
using Monica.OpenTelemetry.UI.UIOpenTelemetry.Pages;
using Monica.OpenTelemetry.UI.UIOpenTelemetry.State;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions used to register the Monica OpenTelemetry dashboard UI module.
/// </summary>
public static class ModuleOpenTelemetryUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the OpenTelemetry dashboard UI module and applies optional module configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The module guide used to continue OpenTelemetry UI registration.</returns>
        public ModuleOpenTelemetryUIGuide AddOpenTelemetryUI(Action<ModuleOpenTelemetryUIOption>? action = null)
        {
            return builder.AddModule<ModuleOpenTelemetryUI, ModuleOpenTelemetryUIOption, ModuleOpenTelemetryUIGuide>(action);
        }
    }
}

/// <summary>
/// UI module that contributes the OpenTelemetry metrics dashboard and enables Monica's in-process collector.
/// </summary>
/// <param name="option">The module configuration options.</param>
[ModuleKey(BuiltInModuleKey.OpenTelemetryUI)]
public class ModuleOpenTelemetryUI(ModuleOpenTelemetryUIOption option)
    : ModuleBase<ModuleOpenTelemetryUI, ModuleOpenTelemetryUIOption, ModuleOpenTelemetryUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<OpenTelemetryUIResource>();

        DependsOnModule<ModuleOpenTelemetryGuide>()
            .Register()
            .UseInProcessCollector();

        if (Option.DisableDashboardPage)
        {
            return;
        }

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIOpenTelemetryDashboardPage, OpenTelemetryUIResource>(
                UIOpenTelemetryDashboardPage.PAGE_URL,
                "Pages:OpenTelemetryMetrics:Title",
                Icons.Material.Filled.MonitorHeart,
                BuiltInNavigationCategoryIds.Monitor,
                addToNav: true,
                navOrder: 12));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<OpenTelemetryDashboardPageState>();
    }
}

/// <summary>
/// Fluent registration guide for the Monica OpenTelemetry dashboard UI module.
/// </summary>
public class ModuleOpenTelemetryUIGuide
    : ModuleGuide<ModuleOpenTelemetryUI, ModuleOpenTelemetryUIOption, ModuleOpenTelemetryUIGuide>;

/// <summary>
/// Configuration options for the Monica OpenTelemetry dashboard UI module.
/// </summary>
public class ModuleOpenTelemetryUIOption : ModuleOptions<ModuleOpenTelemetryUI>
{
    /// <summary>
    /// Gets or sets whether the dashboard page should be excluded from the Monica shell navigation and routing assembly registry.
    /// The underlying in-process collector dependency is still enabled for snapshot API usage.
    /// </summary>
    public bool DisableDashboardPage { get; set; }
}
