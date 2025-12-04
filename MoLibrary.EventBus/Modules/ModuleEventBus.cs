using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Models;
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
internal class EventBusInitializationService : IHostedService
{
    private readonly IMoEventBus _eventBus;
    private readonly ModuleEventBusOption _option;
    private readonly IServiceProvider _serviceProvider;

    public EventBusInitializationService(
        IMoEventBus eventBus,
        ModuleEventBusOption option,
        IServiceProvider serviceProvider)
    {
        _eventBus = eventBus;
        _option = option;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Convert auto-discovered EventHandlerRegisterInfo to Subscriptions
        var descriptors = new List<SubscriptionDescriptor>();

        foreach (var handlerInfo in _option.EventHandlers.Where(h => h.IsAutoRegistered))
        {
            var descriptor = new SubscriptionDescriptor
            {
                ServiceKey = null, // Auto-discovered handlers register to default EventBus
                EventType = handlerInfo.EventType,
                TopicName = handlerInfo.TopicName,
                HandlerFactory = new IocEventHandlerFactory(
                    _serviceProvider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                    handlerInfo.HandlerType),
                Scope = handlerInfo.IsDistributed ? SubscriptionScope.Distributed : SubscriptionScope.Local,
                HandlerType = handlerInfo.HandlerType,
                IsAutoDiscovered = true
            };

            descriptors.Add(descriptor);
        }

        // Batch subscribe
        if (descriptors.Any())
        {
            await _eventBus.Subscriptions.SubscribeBatchAsync(descriptors);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
