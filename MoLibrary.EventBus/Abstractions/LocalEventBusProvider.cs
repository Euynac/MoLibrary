using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Models;
using MoLibrary.EventBus.Modules;
using MoLibrary.Tool.Utils;

namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// Implements EventBus as Singleton pattern.
/// </summary>
public class LocalEventBusProvider(
    IOptions<ModuleEventBusOption> options,
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ILogger<LocalEventBusProvider> logger)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker), IMoLocalEventBus
{
    protected ModuleEventBusOption Options { get; } = options.Value;

    public override IEnumerable<EventHandlerRegisterInfo> GetAutoRegisteredHandlers()
    {
        return Options.EventHandlers.Where(h => h is
        {
            IsDistributed: false, IsLocal: true, IsAutoRegistered: true
        });
    }

  
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await TriggerHandlersAsync(eventType, eventData);
    }
}