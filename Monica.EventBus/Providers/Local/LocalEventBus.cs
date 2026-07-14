using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Services.Support;

namespace Monica.EventBus.Providers.Local;

/// <summary>
/// Local (in-process) event bus implementation.
/// Events are published and handled synchronously within the same process.
/// </summary>
/// <param name="serviceScopeFactory">Creates scopes for event handlers.</param>
/// <param name="eventHandlerInvoker">Invokes resolved event handlers.</param>
/// <param name="subscriptionManager">Owns this host's subscription catalog.</param>
/// <param name="loggerFactory">Creates the event bus logger.</param>
/// <param name="serviceKey">An optional keyed-provider identifier.</param>
public class LocalEventBus(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    IEventSubscriptionRegistry subscriptionManager,
    ILoggerFactory loggerFactory,
    string? serviceKey = null)
    : LocalEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, loggerFactory, serviceKey);
