using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Tool.Utils;

namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// Implements EventBus as Singleton pattern.
/// </summary>
public class LocalEventBus(
    IOptions<LocalEventBusOptions> options,
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ILogger<LocalEventBus> logger)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker), IMoLocalEventBus
{
    /// <summary>
    /// Reference to the Logger.
    /// </summary>
    public ILogger<LocalEventBus> Logger { get; set; } = logger;

    protected LocalEventBusOptions Options { get; } = options.Value;

    public override ITypeList<IMoEventHandler> GetDefaultHandlers()
    {
        return Options.Handlers;
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

    protected override async Task BulkPublishToEventBusAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        await PublishToEventBusAsync(eventType, eventDataList);
    }
}