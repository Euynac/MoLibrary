using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Base class for distributed (cross-process) event bus implementations.
/// Publishes events by sending them to external infrastructure (Dapr, RabbitMQ, etc.).
/// Derived classes must implement the actual publishing logic.
/// </summary>
public abstract class DistributedEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, serviceKey),
        IDistributedEventBus
{
    // Abstract methods - derived classes (DaprEventBus, RabbitMqEventBus, etc.) implement these
}
