using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Base class for local (in-process) event bus implementations.
/// Publishes events by directly triggering handlers in the same process.
/// </summary>
// TODO: Consider reworking this implementation to use the built-in Channel APIs.
public abstract class LocalEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, serviceKey), ILocalEventBus
{
    /// <summary>
    /// For the local event bus, publishing means triggering handlers directly.
    /// </summary>
    public override async Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        await TriggerHandlersAsync(eventType, eventData, finalTopicName, cancellationToken);
    }

    /// <summary>
    /// For the local event bus, bulk publishing means triggering handlers for each event.
    /// </summary>
    public override async Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        foreach (var eventData in eventDataList)
        {
            await TriggerHandlersAsync(eventType, eventData, finalTopicName, cancellationToken);
        }
    }
}
