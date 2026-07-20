using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Annotations;
using Monica.EventBus.Services;
using Monica.EventBus.Services.Support;

namespace Monica.Testing.Doubles;

/// <summary>
/// Event bus double that records published events and can still invoke in-process subscriptions.
/// </summary>
public sealed class RecordingEventBus : ILocalEventBus, IDistributedEventBus
{
    private readonly LocalRecordingEventBus _local;

    /// <summary>
    /// Initializes a recording event bus that dispatches through the host service provider.
    /// </summary>
    /// <param name="serviceScopeFactory">The scope factory used to resolve subscribed handlers.</param>
    /// <param name="eventHandlerInvoker">The event handler invoker used by Monica's local event bus.</param>
    /// <param name="subscriptionRegistry">The shared event subscription registry.</param>
    /// <param name="loggerFactory">The host-owned logger factory.</param>
    public RecordingEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker,
        IEventSubscriptionRegistry subscriptionRegistry,
        ILoggerFactory loggerFactory)
    {
        _local = new LocalRecordingEventBus(serviceScopeFactory, eventHandlerInvoker, subscriptionRegistry, loggerFactory);
    }

    /// <summary>
    /// Gets all recorded publish operations.
    /// </summary>
    public IReadOnlyList<RecordedEvent> Events => _local.Events;

    /// <inheritdoc />
    public IEventSubscriptionRegistry Subscriptions => _local.Subscriptions;

    /// <summary>
    /// Gets recorded events assignable to <typeparamref name="TEvent"/>.
    /// </summary>
    public IReadOnlyList<TEvent> Recorded<TEvent>()
        where TEvent : class
    {
        return Events.Select(static recorded => recorded.EventData).OfType<TEvent>().ToList();
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent eventData, string? topicName = null, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return _local.PublishAsync(eventData, topicName, cancellationToken);
    }

    /// <inheritdoc />
    public Task BulkPublishAsync<TEvent>(
        IEnumerable<TEvent> eventDataList,
        string? topicName = null,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return _local.BulkPublishAsync(eventDataList, topicName, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEventSubscription> SubscribeAsync<TEvent, THandler>(string? topicName = null)
        where TEvent : class
        where THandler : Monica.EventBus.Abstractions.Handlers.IEventHandler
    {
        return _local.SubscribeAsync<TEvent, THandler>(topicName);
    }

    /// <inheritdoc />
    public Task<IEventSubscription> SubscribeAsync<TEvent>(Func<TEvent, Task> handler, string? topicName = null)
        where TEvent : class
    {
        return _local.SubscribeAsync(handler, topicName);
    }

    /// <inheritdoc />
    public Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default)
    {
        return _local.PublishAsync(eventType, eventData, topicName, cancellationToken);
    }

    /// <inheritdoc />
    public Task BulkPublishAsync(
        Type eventType,
        IEnumerable<object> eventDataList,
        string? topicName = null,
        CancellationToken cancellationToken = default)
    {
        return _local.BulkPublishAsync(eventType, eventDataList, topicName, cancellationToken);
    }

    private sealed class LocalRecordingEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IEventHandlerInvoker eventHandlerInvoker,
        IEventSubscriptionRegistry subscriptionManager,
        ILoggerFactory loggerFactory)
        : LocalEventBusBase(serviceScopeFactory, eventHandlerInvoker, subscriptionManager, loggerFactory)
    {
        private readonly List<RecordedEvent> _events = [];

        public IReadOnlyList<RecordedEvent> Events => _events;

        public override async Task PublishAsync(
            Type eventType,
            object eventData,
            string? topicName = null,
            CancellationToken cancellationToken = default)
        {
            var finalTopicName = topicName ?? EventNameAttribute.GetNameOrDefault(eventType);
            _events.Add(new RecordedEvent(eventType, eventData, finalTopicName));
            await base.PublishAsync(eventType, eventData, topicName, cancellationToken);
        }
    }
}

/// <summary>
/// Captures a single event bus publish operation.
/// </summary>
/// <param name="EventType">The published event runtime type.</param>
/// <param name="EventData">The published event payload.</param>
/// <param name="TopicName">The resolved topic name.</param>
public sealed record RecordedEvent(Type EventType, object EventData, string TopicName);
