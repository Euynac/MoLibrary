using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Providers.NoOp;

/// <summary>
/// Null implementation of distributed event bus for testing or scenarios where event publishing is not needed.
/// All publish operations are no-ops.
/// </summary>
/// <param name="serviceScopeFactory">Creates scopes for event handlers.</param>
/// <param name="eventHandlerInvoker">Invokes resolved event handlers.</param>
/// <param name="subscriptionManager">Owns this host's subscription catalog.</param>
/// <param name="loggerFactory">Creates the event bus logger.</param>
public sealed class NoOpDistributedEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    ILoggerFactory loggerFactory)
    : DistributedEventBusBase(serviceScopeFactory,
        eventHandlerInvoker,
        subscriptionManager,
        loggerFactory,
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
