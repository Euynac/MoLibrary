using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Abstractions;

// TODO: Consider reworking this implementation to use the built-in Channel APIs.
/// <summary>
/// Base class for local (in-process) event bus implementations.
/// Publishes events by directly triggering handlers in the same process.
/// </summary>
/// <param name="serviceScopeFactory">Creates scopes for event handlers.</param>
/// <param name="eventHandlerInvoker">Invokes resolved event handlers.</param>
/// <param name="subscriptionManager">Owns this host's subscription catalog.</param>
/// <param name="loggerFactory">Creates the logger for the concrete event bus.</param>
/// <param name="serviceKey">An optional keyed-provider identifier.</param>
public abstract class LocalEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    ILoggerFactory loggerFactory,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, loggerFactory, serviceKey), ILocalEventBus
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
