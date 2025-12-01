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
    /// <summary>
    /// Reference to the Logger.
    /// </summary>
    public ILogger<LocalEventBusProvider> Logger { get; set; } = logger;

    protected ModuleEventBusOption Options { get; } = options.Value;

    public override IEnumerable<EventHandlerRegisterInfo> GetDefaultHandlers()
    {
        return Options.EventHandlers.Where(h => h.IsLocal);
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe<TEvent>(IMoLocalEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }
 
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await TriggerHandlersAsync(eventType, eventData);
    }
}