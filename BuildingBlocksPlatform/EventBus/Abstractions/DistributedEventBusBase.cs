using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocksPlatform.EventBus.Abstractions;

public abstract class DistributedEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<DistributedEventBusOptions> options,
    IEventHandlerInvoker eventHandlerInvoker,
    ILocalEventBus localEventBus) : EventBusBase(
    serviceScopeFactory,
    eventHandlerInvoker), IDistributedEventBus
{
    protected DistributedEventBusOptions EventBusOptions { get; } = options.Value;
    protected ILocalEventBus LocalEventBus { get; } = localEventBus;

    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }

    public override async Task PublishAsync(
        Type eventType,
        object eventData)
    {
        await PublishToEventBusAsync(eventType, eventData);
    }

    protected virtual async Task TriggerHandlersDirectAsync(Type eventType, object eventData)
    {
       
        await TriggerHandlersAsync(eventType, eventData);
    }
}


public sealed class NullDistributedEventBus(IServiceScopeFactory serviceScopeFactory, IOptions<DistributedEventBusOptions> options, IEventHandlerInvoker eventHandlerInvoker, ILocalEventBus localEventBus) : DistributedEventBusBase(serviceScopeFactory, options, eventHandlerInvoker, localEventBus)
{
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        return;
    }

    protected override async Task BulkPublishToEventBusAsync(Type eventType, IEnumerable<object> eventDataList)
    {
        return;
    }
}