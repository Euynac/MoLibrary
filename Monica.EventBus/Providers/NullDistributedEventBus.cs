using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Abstractions.Subscriptions;

namespace Monica.EventBus.Providers;

/// <summary>
/// Null implementation of distributed event bus for testing or scenarios where event publishing is not needed.
/// All publish operations are no-ops.
/// </summary>
public sealed class NullDistributedEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ISubscriptionManager subscriptionManager)
    : DistributedEventBusBase(serviceScopeFactory,
        eventHandlerInvoker,
        subscriptionManager,
        serviceKey: null)
{
    /// <summary>
    /// Null implementation - does nothing.
    /// </summary>
    public override Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Null implementation - does nothing.
    /// </summary>
    public override Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
