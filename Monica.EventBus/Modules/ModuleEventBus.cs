using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
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
    extension(Mo)
    {
        /// <summary>
        /// Configures the EventBus module.
        /// </summary>
        public static ModuleEventBusGuide AddEventBus(Action<ModuleEventBusOption>? action = null)
        {
            return new ModuleEventBusGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.EventBus)]
public class ModuleEventBus(ModuleEventBusOption option)
    : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option),
      IWantIterateBusinessTypes
{
    private readonly EventBusAutoDiscovery _autoDiscovery = new();

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register core EventBus services (shared across all EventBus instances)
        services.AddSingleton<IEventSubscriptionRegistry, EventSubscriptionRegistry>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        services.AddSingleton<ILocalEventBus, LocalEventBus>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var sp = app.ApplicationServices;
        var subscriptionRegistry = sp.GetRequiredService<IEventSubscriptionRegistry>();
        var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var descriptors = _autoDiscovery.BuildDescriptors(serviceScopeFactory);

        // Batch subscribe synchronously during module initialization
        if (descriptors.Count != 0)
        {
            subscriptionRegistry.SubscribeBatchAsync(descriptors).GetAwaiter().GetResult();
        }
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (Option.DisableAutoDiscovery)
            {
                yield return type;
                continue;
            }

            if (type is { IsClass: true, IsAbstract: false } && type.IsImplementInterface<IEventHandler>())
            {
                try
                {
                    _autoDiscovery.Collect(type);
                }
                catch (InvalidOperationException ex)
                {
                    // Fail fast with clear error at startup
                    throw new InvalidOperationException(
                        $"Failed to register event handler '{type.GetCleanFullName()}': {ex.Message}", ex);
                }
            }

            yield return type;
        }
    }
}

public class ModuleEventBusGuide : MoModuleGuide<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>
{
    /// <summary>
    /// Registers the shared distributed event bus provider for the default EventBus instance
    /// where <c>ServiceKey</c> is <see langword="null"/>.
    /// </summary>
    public ModuleEventBusGuide UseDistributedEventBus<TProvider>() where TProvider : DistributedEventBusBase
    {
        ConfigureServices(services =>
        {
            // Register concrete type first
            services.Services.AddSingleton<TProvider>();
            // Register interface pointing to concrete type
            services.Services.AddSingleton<IDistributedEventBus>(sp => sp.GetRequiredService<TProvider>());
        });
        return this;
    }

    /// <summary>
    /// Registers a no-op distributed event bus for testing or scenarios where external event
    /// publishing is not required.
    /// </summary>
    public ModuleEventBusGuide UseNoOpDistributedEventBus()
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IDistributedEventBus, NoOpDistributedEventBus>();
        });
        return this;
    }

    /// <summary>
    /// Registers a keyed <see cref="IEventBus"/> and maps it to either the local or distributed
    /// implementation based on <paramref name="useDistributed"/>.
    /// </summary>
    /// <param name="key">Service key.</param>
    /// <param name="useDistributed">
    /// <see langword="true"/> to use the distributed event bus; otherwise, the local event bus.
    /// </param>
    public ModuleEventBusGuide AddKeyedEventBus(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
        {
            if (useDistributed)
            {
                CheckRequiredMethod(nameof(UseDistributedEventBus));
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
        }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }

    /// <summary>
    /// Registers a keyed local event bus backed by a <see cref="LocalEventBus"/> instance that
    /// carries the specified service key.
    /// </summary>
    /// <param name="key">Service key.</param>
    public ModuleEventBusGuide AddKeyedLocalEventBus(string key)
    {
        ConfigureServices(context =>
        {
            // Register keyed LocalEventBus with the specified serviceKey
            context.Services.AddKeyedSingleton<ILocalEventBus>(key, (sp, _) =>
                ActivatorUtilities.CreateInstance<LocalEventBus>(sp, key));
        }, secondKey: key);

        RecordKeyedServiceKey(key);
        return this;
    }
}

public class ModuleEventBusOption : MoModuleOptionWithMinimalApi<ModuleEventBus>
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic discovery is disabled for types that
    /// implement <see cref="IDistributedEventHandler{TEvent}"/> or
    /// <see cref="ILocalEventHandler{TEvent}"/>.
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }
}
