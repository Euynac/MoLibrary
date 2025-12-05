using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Models;
using MoLibrary.EventBus.Providers;
using MoLibrary.EventBus.Services;
using MoLibrary.EventBus.Subscriptions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option)
    : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option),
      IWantIterateBusinessTypes
{
    private readonly List<EventHandlerRegisterInfo> _autoDiscoveredHandlers = [];

    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBus;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Register core EventBus services (shared across all EventBus instances)
        services.AddSingleton<ISubscriptionManager, SubscriptionManager>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();

        services.AddSingleton<LocalEventBus>();
        services.AddSingleton<IMoLocalEventBus>(sp => sp.GetRequiredService<LocalEventBus>());
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