using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.EventBus.Kafka.Localization;
using Monica.EventBus.Kafka.Pages;
using Monica.EventBus.Kafka.UIEventBusKafka.State;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Kafka EventBus console UI module.
/// </summary>
public static class ModuleEventBusKafkaUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the Kafka EventBus console UI.
        /// </summary>
        /// <param name="action">Optional UI module option configuration.</param>
        /// <returns>The Kafka EventBus UI guide used for chained configuration.</returns>
        public static ModuleEventBusKafkaUIGuide AddEventBusKafkaUI(Action<ModuleEventBusKafkaUIOption>? action = null)
        {
            return new ModuleEventBusKafkaUIGuide().Register(action);
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
            .RegisterUIComponents(registry => registry.RegisterLocalizedComponent<UIEventBusKafkaPage>(
                UIEventBusKafkaPage.PAGE_URL,
                "Pages:EventBusKafka:Title",
                Icons.Material.Filled.Storage,
                "Categories:Infrastructure",
                addToNav: true,
                navOrder: 38));
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<EventBusKafkaPageState>();
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
}
