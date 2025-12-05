using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions.Handlers;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Helpers;
using MoLibrary.EventBus.Models;
using MoLibrary.EventBus.Subscriptions;

namespace MoLibrary.EventBus.Abstractions;

/// <summary>
/// Base class for all EventBus implementations.
/// Provides common subscription management and handler triggering functionality.
/// </summary>
public abstract class EventBusBase(
    IServiceScopeFactory serviceScopeFactory,
    IEventHandlerInvoker eventHandlerInvoker,
    ISubscriptionManager subscriptionManager,
    ILogger logger,
    string? serviceKey = null)
    : IMoEventBus
{
    protected readonly IServiceScopeFactory ServiceScopeFactory = serviceScopeFactory;
    protected readonly IEventHandlerInvoker EventHandlerInvoker = eventHandlerInvoker;
    protected readonly ISubscriptionManager SubscriptionManager = subscriptionManager;
    protected readonly ILogger Logger = logger;
    protected readonly string? ServiceKey = serviceKey;

    public ISubscriptionManager Subscriptions => SubscriptionManager;

    #region Subscribe Methods

    public virtual ISubscription Subscribe<TEvent, THandler>(string? topicName = null)
        where TEvent : class
        where THandler : IMoEventHandler
    {
        var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(typeof(TEvent));
        var descriptor = new SubscriptionDescriptor
        {
            ServiceKey = ServiceKey,
            EventType = typeof(TEvent),
            TopicName = finalTopicName,
            HandlerFactory = new IocEventHandlerFactory(ServiceScopeFactory, typeof(THandler)),
            Scope = this is IMoLocalEventBus ? SubscriptionScope.Local : SubscriptionScope.Distributed,
            IsAutoDiscovered = false
        };
        return SubscriptionManager.SubscribeAsync(descriptor).GetAwaiter().GetResult();
    }

    public virtual ISubscription Subscribe<TEvent>(Func<TEvent, Task> handler, string? topicName = null)
        where TEvent : class
    {
        var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(typeof(TEvent));

        // 提取 Action 处理器的元数据
        var metadata = DelegateMetadataExtractor.ExtractMetadata(handler);

        var descriptor = new SubscriptionDescriptor
        {
            ServiceKey = ServiceKey,
            EventType = typeof(TEvent),
            TopicName = finalTopicName,
            HandlerFactory = new ActionEventHandlerFactory<TEvent>(handler),
            Scope = this is IMoLocalEventBus ? SubscriptionScope.Local : SubscriptionScope.Distributed,
            IsAutoDiscovered = false,
            Metadata = metadata
        };
        return SubscriptionManager.SubscribeAsync(descriptor).GetAwaiter().GetResult();
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
        var isLocal = this is IMoLocalEventBus;
        // Query active subscriptions for this event type and topic
        var subscriptions = SubscriptionManager.GetAll()
            .Where(s => s.EventType == eventType &&
                        s.TopicName == topicName &&
                        s.State == SubscriptionState.Active &&
                        s.ServiceKey == ServiceKey && s.Scope == (isLocal ? SubscriptionScope.Local : SubscriptionScope.Distributed))
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
