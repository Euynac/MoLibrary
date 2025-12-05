using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;

namespace MoLibrary.EventBus;

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
