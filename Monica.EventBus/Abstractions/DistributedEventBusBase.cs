using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Abstractions.Subscriptions;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Base class for distributed (cross-process) event bus implementations.
/// Publishes events by sending them to external infrastructure (Dapr, RabbitMQ, etc.).
/// Derived classes must implement the actual publishing logic.
/// </summary>
public abstract class DistributedEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ISubscriptionManager subscriptionManager,
    ILogger logger,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, logger, serviceKey),
        IMoDistributedEventBus
{
    // Abstract methods - derived classes (DaprEventBus, RabbitMqEventBus, etc.) implement these
}
