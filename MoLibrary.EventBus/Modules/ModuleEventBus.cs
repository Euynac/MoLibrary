using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Models;
using MoLibrary.EventBus.Providers;
using MoLibrary.EventBus.Subscriptions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option)
    : MoModule<ModuleEventBus, ModuleEventBusOption, ModuleEventBusGuide>(option),
      IWantIterateBusinessTypes
{
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

        // Register hosted service to initialize auto-discovered subscriptions
        services.AddHostedService<EventBusInitializationService>();
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
                        Option.EventHandlers.Add(registration);
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

/// <summary>
/// Hosted service that initializes auto-discovered subscriptions on application startup.
/// </summary>
internal class EventBusInitializationService(
    ISubscriptionManager subscriptionManager,
    IOptions<ModuleEventBusOption> option,
    IServiceProvider serviceProvider)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Convert auto-discovered EventHandlerRegisterInfo to Subscriptions
        var descriptors = option.Value.EventHandlers.Where(h => h.IsAutoRegistered)
            .Select(handlerInfo => new SubscriptionDescriptor
            {
                ServiceKey = null, // Auto-discovered handlers register to default EventBus
                EventType = handlerInfo.EventType,
                TopicName = handlerInfo.TopicName,
                HandlerFactory = new IocEventHandlerFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>(), handlerInfo.HandlerType),
                Scope = handlerInfo.IsDistributed ? SubscriptionScope.Distributed : SubscriptionScope.Local,
                IsAutoDiscovered = true
            })
            .ToList();

        // Batch subscribe
        if (descriptors.Count != 0)
        {
            await subscriptionManager.SubscribeBatchAsync(descriptors);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
