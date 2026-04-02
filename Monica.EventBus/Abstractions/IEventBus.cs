using Monica.EventBus.Abstractions.Handlers;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Core EventBus interface - unified for both local and distributed scenarios.
/// Provides publishing and subscription management capabilities.
/// </summary>
public interface IEventBus
{
    #region Publishing

    /// <summary>
    /// Publishes an event to the event bus.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="eventData">Event data</param>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<TEvent>(TEvent eventData, string? topicName = null, CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// Publishes multiple events in bulk for optimized throughput.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="eventDataList">Collection of event data</param>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BulkPublishAsync<TEvent>(IEnumerable<TEvent> eventDataList, string? topicName = null, CancellationToken cancellationToken = default)
        where TEvent : class;

    #endregion

    #region EventSubscription Management

    /// <summary>
    /// Gets the subscription manager for advanced subscription operations.
    /// </summary>
    IEventSubscriptionRegistry Subscriptions { get; }

    /// <summary>
    /// Simple subscription helper - subscribes to an event with a handler type.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <returns>The created subscription</returns>
    Task<IEventSubscription> SubscribeAsync<TEvent, THandler>(string? topicName = null)
        where TEvent : class
        where THandler : IEventHandler;

    /// <summary>
    /// Simple subscription helper - subscribes to an event with an action.
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <param name="handler">Handler action</param>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <returns>The created subscription</returns>
    Task<IEventSubscription> SubscribeAsync<TEvent>(Func<TEvent, Task> handler, string? topicName = null)
        where TEvent : class;

    #endregion
    

    #region Non-Generic Publishing (Advanced)

    /// <summary>
    /// Publishes an event using runtime type information.
    /// This is an advanced method for scenarios where the event type is only known at runtime.
    /// Prefer using the generic PublishAsync{TEvent} method when possible.
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="eventData">Event data</param>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync(Type eventType, object eventData, string? topicName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events in bulk using runtime type information.
    /// This is an advanced method for scenarios where the event type is only known at runtime.
    /// Prefer using the generic BulkPublishAsync{TEvent} method when possible.
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="eventDataList">Collection of event data</param>
    /// <param name="topicName">Optional custom topic name (overrides EventNameAttribute)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task BulkPublishAsync(Type eventType, IEnumerable<object> eventDataList, string? topicName = null, CancellationToken cancellationToken = default);

    #endregion
}
