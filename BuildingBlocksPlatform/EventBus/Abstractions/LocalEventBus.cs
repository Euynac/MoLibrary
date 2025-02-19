using BuildingBlocksPlatform.Utils;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;



namespace BuildingBlocksPlatform.EventBus.Abstractions;

/// <summary>
/// Implements EventBus as Singleton pattern.
/// </summary>
[ExposeServices(typeof(ILocalEventBus), typeof(LocalEventBus))]
public class LocalEventBus(
    IOptions<LocalEventBusOptions> options,
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ILogger<LocalEventBus> logger)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker), ILocalEventBus, ISingletonDependency
{
    /// <summary>
    /// Reference to the Logger.
    /// </summary>
    public ILogger<LocalEventBus> Logger { get; set; } = logger;

    protected LocalEventBusOptions Options { get; } = options.Value;

    public override ITypeList<IEventHandler> GetDefaultHandlers()
    {
        return Options.Handlers;
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
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