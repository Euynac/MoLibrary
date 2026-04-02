using Microsoft.Extensions.DependencyInjection;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Providers.Local;

/// <summary>
/// Local (in-process) event bus implementation.
/// Events are published and handled synchronously within the same process.
/// </summary>
public class LocalEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    string? serviceKey = null)
    : LocalEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, serviceKey);
