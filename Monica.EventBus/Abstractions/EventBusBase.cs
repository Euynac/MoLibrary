using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.EventBus.Abstractions.Handlers;
using Monica.EventBus.Annotations;
using Monica.EventBus.Models;
using Monica.EventBus.Services.Support;
using Monica.EventBus.Utils;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Base class for all EventBus implementations.
/// Provides common subscription management and handler triggering functionality.
/// </summary>
public abstract class EventBusBase : IEventBus
{
    private readonly Lazy<ILogger> _loggerLazy;

    protected EventBusBase(
        IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker,
        IEventSubscriptionRegistry subscriptionManager,
        string? serviceKey = null)
    {
        ServiceScopeFactory = serviceScopeFactory;
        EventHandlerInvoker = eventHandlerInvoker;
        SubscriptionManager = subscriptionManager;
        ServiceKey = serviceKey;
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    protected IServiceScopeFactory ServiceScopeFactory { get; }
    protected IEventHandlerInvoker EventHandlerInvoker { get; }
    protected IEventSubscriptionRegistry SubscriptionManager { get; }
    protected ILogger Logger => _loggerLazy.Value;
    protected string? ServiceKey { get; }

    public IEventSubscriptionRegistry Subscriptions => SubscriptionManager;

    #region Subscribe Methods

    public virtual async Task<IEventSubscription> SubscribeAsync<TEvent, THandler>(string? topicName = null)
        where TEvent : class
        where THandler : IEventHandler
    {
        var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(typeof(TEvent));
        var descriptor = new EventSubscriptionDescriptor
        {
            ServiceKey = ServiceKey,
            EventType = typeof(TEvent),
            TopicName = finalTopicName,
            HandlerFactory = new IocEventHandlerFactory(ServiceScopeFactory, typeof(THandler)),
            Scope = this is ILocalEventBus ? EventSubscriptionScope.Local : EventSubscriptionScope.Distributed,
            IsAutoDiscovered = false
        };
        return await SubscriptionManager.SubscribeAsync(descriptor);
    }

    public virtual async Task<IEventSubscription> SubscribeAsync<TEvent>(Func<TEvent, Task> handler, string? topicName = null)
        where TEvent : class
    {
        var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(typeof(TEvent));

        // Extract metadata for the action-based handler.
        var metadata = DelegateMetadataExtractor.ExtractMetadata(handler);

        var descriptor = new EventSubscriptionDescriptor
        {
            ServiceKey = ServiceKey,
            EventType = typeof(TEvent),
            TopicName = finalTopicName,
            HandlerFactory = new ActionEventHandlerFactory<TEvent>(handler),
            Scope = this is ILocalEventBus ? EventSubscriptionScope.Local : EventSubscriptionScope.Distributed,
            IsAutoDiscovered = false,
            Metadata = metadata
        };
        return await SubscriptionManager.SubscribeAsync(descriptor);
    }

    #endregion

    #region Publish Methods

    public virtual Task PublishAsync<TEvent>(TEvent eventData, string? topicName = null, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData, topicName, cancellationToken);
    }

    public abstract Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default);

    public virtual Task BulkPublishAsync<TEvent>(IEnumerable<TEvent> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return BulkPublishAsync(typeof(TEvent), eventDataList, topicName, cancellationToken);
    }

    public abstract Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default);

    #endregion

    #region Trigger Handlers

    public virtual async Task TriggerHandlersAsync(Type eventType, object eventData, string topicName, CancellationToken cancellationToken = default)
    {
        var isLocal = this is ILocalEventBus;
        // Query active subscriptions for this event type and topic
        var subscriptions = SubscriptionManager.GetAll()
            .Where(s => s.EventType == eventType &&
                        s.TopicName == topicName &&
                        s.State == EventSubscriptionState.Active &&
                        s.ServiceKey == ServiceKey && s.Scope == (isLocal ? EventSubscriptionScope.Local : EventSubscriptionScope.Distributed))
            .ToList();

        if (subscriptions.Count == 0)
        {
            Logger.LogDebug(
                "No active subscriptions found for event {EventType} on topic {Topic}",
                eventType.Name, topicName);
            return;
        }

        Logger.LogDebug(
            "Triggering {Count} handlers for event {EventType} on topic {Topic}",
            subscriptions.Count, eventType.Name, topicName);

        // Invoke each handler
        foreach (var subscription in subscriptions)
        {
            try
            {
                using var handlerWrapper = subscription.HandlerFactory.GetHandler();
                await EventHandlerInvoker.InvokeAsync(handlerWrapper.EventHandler, eventData, eventType);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error invoking handler {HandlerType} for event {EventType}",
                    subscription.HandlerType?.Name ?? "Unknown", eventType.Name);
                throw;
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Resolves the topic name for an event type, using custom topic if provided,
    /// otherwise falling back to EventNameAttribute.
    /// </summary>
    protected static string ResolveTopicName(Type eventType, string? topicName)
    {
        var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(eventType);
        return finalTopicName;
    }

    #endregion
}
