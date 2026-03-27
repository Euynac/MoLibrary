using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Abstractions.Subscriptions;
using Monica.EventBus.Models;
using Monica.EventBus.Providers;
using Monica.EventBus.Subscriptions;
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
    private readonly List<EventHandlerRegisterInfo> _autoDiscoveredHandlers = [];

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register core EventBus services (shared across all EventBus instances)
        services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        services.AddSingleton<IMoLocalEventBus, LocalEventBus>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var sp = app.ApplicationServices;
        var subscriptionManager = sp.GetRequiredService<ISubscriptionManager>();
        var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        // Convert auto-discovered EventHandlerRegisterInfo to Subscriptions
        var descriptors = _autoDiscoveredHandlers.Where(h => h.IsAutoRegistered)
            .Select(handlerInfo => new SubscriptionDescriptor
            {
                ServiceKey = null, // Auto-discovered handlers register to default EventBus
                EventType = handlerInfo.EventType,
                TopicName = handlerInfo.TopicName,
                HandlerFactory = new IocEventHandlerFactory(serviceScopeFactory, handlerInfo.HandlerType),
                Scope = handlerInfo.IsDistributed ? SubscriptionScope.Distributed : SubscriptionScope.Local,
                IsAutoDiscovered = true
            })
            .ToList();

        // Batch subscribe synchronously during module initialization
        if (descriptors.Count != 0)
        {
            subscriptionManager.SubscribeBatchAsync(descriptors).GetAwaiter().GetResult();
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
       
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

            if (type is { IsClass: true, IsAbstract: false } && type.IsImplementInterface<IMoEventHandler>())
            {
                try
                {
                    // Use factory method - handles all reflection and validation
                    var registrations = EventHandlerRegisterInfo.CreateFromHandlerType(type);
                    foreach (var registration in registrations)
                    {
                        _autoDiscoveredHandlers.Add(registration);
                    }
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
    public ModuleEventBusGuide SetDistributedEventBusProvider<TProvider>() where TProvider : DistributedEventBusBase
    {
        ConfigureServices(services =>
        {
            // Register concrete type first
            services.Services.AddSingleton<TProvider>();
            // Register interface pointing to concrete type
            services.Services.AddSingleton<IMoDistributedEventBus>(sp => sp.GetRequiredService<TProvider>());
        });
        return this;
    }

    /// <summary>
    /// Registers a no-op distributed event bus for testing or scenarios where external event
    /// publishing is not required.
    /// </summary>
    public ModuleEventBusGuide SetDistributedEventBusNullProvider()
    {
        ConfigureServices(services =>
        {
            services.Services.AddSingleton<IMoDistributedEventBus, NullDistributedEventBus>();
        });
        return this;
    }

    /// <summary>
    /// Registers a keyed <see cref="IMoEventBus"/> and maps it to either the local or distributed
    /// implementation based on <paramref name="useDistributed"/>.
    /// </summary>
    /// <param name="key">Service key.</param>
    /// <param name="useDistributed">
    /// <see langword="true"/> to use the distributed event bus; otherwise, the local event bus.
    /// </param>
    public ModuleEventBusGuide AddKeyedCommonEventBus(string key, bool useDistributed = false)
    {
        ConfigureServices(context =>
        {
            if (useDistributed)
            {
                CheckRequiredMethod(nameof(SetDistributedEventBusProvider));
                // For distributed, delegate to the registered keyed IMoDistributedEventBus
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<IMoDistributedEventBus>());
            }
            else
            {
                // For local, delegate to the registered keyed IMoLocalEventBus
                context.Services.TryAddKeyedSingleton<IMoEventBus>(key, (sp, _) =>
                    sp.GetRequiredService<IMoLocalEventBus>());
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
            context.Services.AddKeyedSingleton<IMoLocalEventBus>(key, (sp, _) =>
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
    /// implement <see cref="IMoDistributedEventHandler{TEvent}"/> or
    /// <see cref="IMoLocalEventHandler{TEvent}"/>.
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }
}
