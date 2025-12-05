using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;

namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// Base class for local (in-process) event bus implementations.
/// Publishes events by directly triggering handlers in the same process.
/// </summary>
public abstract class LocalEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ISubscriptionManager subscriptionManager,
    ILogger logger,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, logger, serviceKey), IMoLocalEventBus
{
    /// <summary>
    /// For local event bus, publishing = directly triggering handlers.
    /// </summary>
    public override async Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        var finalTopicName = ResolveTopicName(eventType, topicName);
        await TriggerHandlersAsync(eventType, eventData, finalTopicName, cancellationToken);
    }

    /// <summary>
    /// For local event bus, bulk publishing = triggering handlers for each event.
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
