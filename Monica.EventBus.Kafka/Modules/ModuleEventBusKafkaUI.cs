using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.EventBus.Kafka.Localization;
using Monica.EventBus.Kafka.Pages;
using Monica.EventBus.Kafka.UIEventBusKafka.State;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Kafka EventBus console UI module.
/// </summary>
public static class ModuleEventBusKafkaUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the Kafka EventBus console UI.
        /// </summary>
        /// <param name="action">Optional UI module option configuration.</param>
        /// <returns>The Kafka EventBus UI guide used for chained configuration.</returns>
        public ModuleEventBusKafkaUIGuide AddEventBusKafkaUI(Action<ModuleEventBusKafkaUIOption>? action = null)
        {
            return builder.AddModule<ModuleEventBusKafkaUI, ModuleEventBusKafkaUIOption, ModuleEventBusKafkaUIGuide>(action);
        }
    }
}

/// <summary>
/// Kafka EventBus management console UI module.
/// </summary>
/// <param name="option">Module options.</param>
[ModuleKey(BuiltInModuleKey.EventBusKafkaUI)]
public sealed class ModuleEventBusKafkaUI(ModuleEventBusKafkaUIOption option)
    : ModuleBase<ModuleEventBusKafkaUI, ModuleEventBusKafkaUIOption, ModuleEventBusKafkaUIGuide>(option)
{
    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusKafkaGuide>().Register();
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<EventBusKafkaResource>();

        if (Option.DisableKafkaConsolePage)
        {
            return;
        }

        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIEventBusKafkaPage, EventBusKafkaResource>(
                UIEventBusKafkaPage.PAGE_URL,
                "Pages:EventBusKafka:Title",
                Icons.Material.Filled.Storage,
                BuiltInNavigationCategoryIds.Infrastructure,
                addToNav: true,
                navOrder: 38));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<EventBusKafkaPageState>();
        services.AddScoped<KafkaPerformancePollingState>();
        services.AddTransient<KafkaConsumerMetricsPollingState>();
    }
}

/// <summary>
/// Fluent guide for the Kafka EventBus console UI module.
/// </summary>
public sealed class ModuleEventBusKafkaUIGuide
    : ModuleGuide<ModuleEventBusKafkaUI, ModuleEventBusKafkaUIOption, ModuleEventBusKafkaUIGuide>
{
}

/// <summary>
/// Configuration options for the Kafka EventBus console UI module.
/// </summary>
public sealed class ModuleEventBusKafkaUIOption : ModuleOptions<ModuleEventBusKafkaUI>
{
    /// <summary>
    /// Gets or sets a value indicating whether the Kafka console page should be hidden from navigation.
    /// </summary>
    public bool DisableKafkaConsolePage { get; set; }

    /// <summary>
    /// Gets or sets whether the Kafka performance tab starts live sampling automatically.
    /// </summary>
    /// <remarks>
    /// The page still provides a manual capture button when this is disabled. The default is
    /// enabled so a newly opened console establishes a rate baseline without extra clicks.
    /// </remarks>
    public bool EnablePerformanceAutoRefresh { get; set; } = true;

    /// <summary>
    /// Gets or sets the default interval used by the live performance sampler.
    /// </summary>
    /// <remarks>
    /// The default is five seconds. Users can change the interval on the performance tab for the
    /// current page session; this option controls the initial value for new sessions.
    /// </remarks>
    public TimeSpan PerformanceRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
}
