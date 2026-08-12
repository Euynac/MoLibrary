using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        /// <returns>The host-bound Kafka EventBus UI registration.</returns>
        public ModuleRegistration<ModuleEventBusKafkaUI, ModuleEventBusKafkaUIOption> AddEventBusKafkaUI(
            Action<ModuleEventBusKafkaUIOption>? action = null)
        {
            return builder.AddModule<ModuleEventBusKafkaUI, ModuleEventBusKafkaUIOption>(action);
        }
    }
}

/// <summary>
/// Kafka EventBus management console UI module.
/// </summary>
public sealed class ModuleEventBusKafkaUI : MonicaModule<ModuleEventBusKafkaUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleEventBusKafka, ModuleEventBusKafkaOption>();
        module.Require<ModuleLocalization, ModuleLocalizationOption>(
            static option => option.AddResource<EventBusKafkaResource>());
        module.Require<ModuleShellUI, ModuleShellUIOption>(static option =>
            option.ConfigureNavigation(static registry =>
                registry.RegisterLocalizedPage<UIEventBusKafkaPage, EventBusKafkaResource>(
                    UIEventBusKafkaPage.PAGE_URL,
                    "Pages:EventBusKafka:Title",
                    Icons.Material.Filled.Storage,
                    BuiltInNavigationCategoryIds.Infrastructure,
                    addToNav: true,
                    navOrder: 38)));
    }

    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleEventBusKafkaUIOption> context)
    {
        var services = context.Services;
        services.AddScoped<EventBusKafkaPageState>();
        services.AddScoped<KafkaPerformancePollingState>();
        services.AddTransient<KafkaConsumerMetricsPollingState>();
    }
}

/// <summary>
/// Configuration options for the Kafka EventBus console UI module.
/// </summary>
public sealed class ModuleEventBusKafkaUIOption : ModuleOptions<ModuleEventBusKafkaUI>
{
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
