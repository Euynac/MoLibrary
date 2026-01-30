using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Abstractions.Subscriptions;

namespace Monica.EventBus.Providers;

/// <summary>
/// Local (in-process) event bus implementation.
/// Events are published and handled synchronously within the same process.
/// </summary>
public class LocalEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ISubscriptionManager subscriptionManager,
    ILogger<LocalEventBus> logger,
    string? serviceKey = null)
    : LocalEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, logger, serviceKey);
