using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Providers.Local;
using Monica.EventBus.Providers.NoOp;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleEventBusBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the EventBus module.
        /// </summary>
        public ModuleRegistration<ModuleEventBus, ModuleEventBusOption> AddEventBus(Action<ModuleEventBusOption>? action = null)
        {
            return builder.AddModule<ModuleEventBus, ModuleEventBusOption>(action);
        }
    }
}

public class ModuleEventBus : MonicaModule<ModuleEventBusOption>
{
    /// <summary>
    /// Identifies the distributed-provider capability required by distributed EventBus features.
    /// </summary>
    public const string DISTRIBUTED_PROVIDER_FEATURE = "distributed-provider";

    private readonly EventBusAutoDiscovery _autoDiscovery = new();

    public override void ConfigureServices(ModuleContext<ModuleEventBusOption> context)
    {
        var services = context.Services;
        // Register core EventBus services (shared across all EventBus instances)
        services.AddSingleton<IEventSubscriptionRegistry, EventSubscriptionRegistry>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        services.AddSingleton<ILocalEventBus, LocalEventBus>();
        services.AddSingleton(_autoDiscovery);
        services.AddHostedService<EventBusAutoDiscoveryLifecycle>();
    }

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>();
    }

    /// <inheritdoc />
    public override void DiscoverTypes(TypeDiscoveryPlan<ModuleEventBusOption> discovery)
    {
        if (Option.DisableAutoDiscovery)
        {
            return;
        }

        discovery.Match(
            TypeQuery.ConcreteClass.AssignableTo<IEventHandler>(),
            (_, matches) =>
            {
                foreach (var match in matches)
                {
                    try
                    {
                        _autoDiscovery.Collect(match.Type);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to register event handler '{match.Type.GetCleanFullName()}': {ex.Message}", ex);
                    }
                }
            });
    }
}

public static class ModuleEventBusRegistrationExtensions
{
    /// <summary>
    /// Registers the shared distributed event bus provider for the default EventBus instance
    /// where <c>ServiceKey</c> is <see langword="null"/>.
    /// </summary>
    public static ModuleRegistration<ModuleEventBus, ModuleEventBusOption> UseDistributedEventBus<TProvider>(this ModuleRegistration<ModuleEventBus, ModuleEventBusOption> module) where TProvider : DistributedEventBusBase
    {
        return module
            .ConfigureServices(context =>
            {
                context.Services.AddSingleton<TProvider>();
                context.Services.AddSingleton<IDistributedEventBus>(sp => sp.GetRequiredService<TProvider>());
            })
            .SatisfyFeature(ModuleEventBus.DISTRIBUTED_PROVIDER_FEATURE);
    }

    /// <summary>
    /// Registers a no-op distributed event bus for testing or scenarios where external event
    /// publishing is not required.
    /// </summary>
    public static ModuleRegistration<ModuleEventBus, ModuleEventBusOption> UseNoOpDistributedEventBus(this ModuleRegistration<ModuleEventBus, ModuleEventBusOption> module)
    {
        return module
            .ConfigureServices(context =>
                context.Services.AddSingleton<IDistributedEventBus, NoOpDistributedEventBus>())
            .SatisfyFeature(ModuleEventBus.DISTRIBUTED_PROVIDER_FEATURE);
    }

    /// <summary>
    /// Registers a keyed <see cref="IEventBus"/> and maps it to either the local or distributed
    /// implementation based on <paramref name="useDistributed"/>.
    /// </summary>
    /// <param name="module">The EventBus module registration.</param>
    /// <param name="key">Service key.</param>
    /// <param name="useDistributed">
    /// <see langword="true"/> to use the distributed event bus; otherwise, the local event bus.
    /// </param>
    public static ModuleRegistration<ModuleEventBus, ModuleEventBusOption> AddKeyedEventBus(this ModuleRegistration<ModuleEventBus, ModuleEventBusOption> module, string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            module.RequireFeature(ModuleEventBus.DISTRIBUTED_PROVIDER_FEATURE);
        }

        module.ConfigureServices(context =>
        {
            if (useDistributed)
            {
                // For distributed, delegate to the registered keyed IDistributedEventBus
                context.Services.TryAddKeyedSingleton<IEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<IDistributedEventBus>());
            }
            else
            {
                // For local, delegate to the registered keyed ILocalEventBus
                context.Services.TryAddKeyedSingleton<IEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<ILocalEventBus>());
            }
        });

        module.RecordKeyedServiceKey(key);
        return module;
    }

    /// <summary>
    /// Registers a keyed local event bus backed by a <see cref="LocalEventBus"/> instance that
    /// carries the specified service key.
    /// </summary>
    /// <param name="module">The EventBus module registration.</param>
    /// <param name="key">Service key.</param>
    public static ModuleRegistration<ModuleEventBus, ModuleEventBusOption> AddKeyedLocalEventBus(this ModuleRegistration<ModuleEventBus, ModuleEventBusOption> module, string key)
    {
        module.ConfigureServices(context =>
        {
            // Register keyed LocalEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<ILocalEventBus>(key, (sp, _) =>
                ActivatorUtilities.CreateInstance<LocalEventBus>(sp, key));
        });

        module.RecordKeyedServiceKey(key);
        return module;
    }

}

public class ModuleEventBusOption : ModuleOptions<ModuleEventBus>
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic discovery is disabled for types that
    /// implement <see cref="IDistributedEventHandler{TEvent}"/> or
    /// <see cref="ILocalEventHandler{TEvent}"/>.
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }
}
