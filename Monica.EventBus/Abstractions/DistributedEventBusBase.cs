using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Base class for distributed (cross-process) event bus implementations.
/// Publishes events by sending them to external infrastructure (Dapr, RabbitMQ, etc.).
/// Derived classes must implement the actual publishing logic.
/// </summary>
/// <param name="serviceScopeFactory">Creates scopes for event handlers.</param>
/// <param name="eventHandlerInvoker">Invokes resolved event handlers.</param>
/// <param name="subscriptionManager">Owns this host's subscription catalog.</param>
/// <param name="loggerFactory">Creates the logger for the concrete event bus.</param>
/// <param name="serviceKey">An optional keyed-provider identifier.</param>
public abstract class DistributedEventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    ILoggerFactory loggerFactory,
    string? serviceKey = null)
    : EventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, loggerFactory, serviceKey),
        IDistributedEventBus
{
    // Abstract methods - derived classes (DaprEventBus, RabbitMqEventBus, etc.) implement these
}
