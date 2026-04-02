using Monica.EventBus.Models;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// Manages all event subscriptions with queryable, observable capabilities.
/// Serves as the single source of truth for subscription state.
/// </summary>
public interface IEventSubscriptionRegistry : IObservable<EventSubscriptionChange>
{
    #region EventSubscription CRUD

    /// <summary>
    /// Creates and activates a new subscription.
    /// </summary>
    /// <param name="descriptor">EventSubscription descriptor</param>
    /// <returns>The created and activated subscription</returns>
    Task<IEventSubscription> SubscribeAsync(EventSubscriptionDescriptor descriptor);

    /// <summary>
    /// Creates and activates multiple subscriptions in a single batch.
    /// </summary>
    /// <param name="descriptors">Collection of subscription descriptors</param>
    /// <returns>Created and activated subscriptions</returns>
    Task<IReadOnlyList<IEventSubscription>> SubscribeBatchAsync(IEnumerable<EventSubscriptionDescriptor> descriptors);

    /// <summary>
    /// Removes and disposes a subscription.
    /// </summary>
    /// <param name="subscriptionId">EventSubscription ID</param>
    Task UnsubscribeAsync(EventSubscriptionId subscriptionId);

    /// <summary>
    /// Removes and disposes multiple subscriptions in a single batch.
    /// </summary>
    /// <param name="subscriptionIds">Collection of subscription IDs</param>
    Task UnsubscribeBatchAsync(IEnumerable<EventSubscriptionId> subscriptionIds);

    /// <summary>
    /// Removes and disposes all subscriptions matching a predicate.
    /// </summary>
    /// <param name="predicate">Filter predicate</param>
    Task UnsubscribeWhereAsync(Func<IEventSubscription, bool> predicate);

    #endregion

    #region Query Operations

    /// <summary>
    /// Gets all subscriptions as a queryable collection.
    /// </summary>
    /// <returns>Queryable subscription collection</returns>
    IQueryable<IEventSubscription> GetAll();

    /// <summary>
    /// Gets a specific subscription by ID.
    /// </summary>
    /// <param name="subscriptionId">EventSubscription ID</param>
    /// <returns>EventSubscription if found, null otherwise</returns>
    IEventSubscription? GetById(EventSubscriptionId subscriptionId);

    /// <summary>
    /// Gets all subscriptions for a specific service key.
    /// </summary>
    /// <param name="serviceKey">Service key (null for default)</param>
    /// <returns>Subscriptions for the service key</returns>
    IReadOnlyList<IEventSubscription> GetByServiceKey(string? serviceKey);

    /// <summary>
    /// Gets all subscriptions for a specific topic name.
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <returns>Subscriptions for the topic</returns>
    IReadOnlyList<IEventSubscription> GetByTopicName(string topicName);

    /// <summary>
    /// Gets all subscriptions for a specific event type.
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <returns>Subscriptions for the event type</returns>
    IReadOnlyList<IEventSubscription> GetByEventType(Type eventType);

    /// <summary>
    /// Gets all subscriptions in a specific state.
    /// </summary>
    /// <param name="state">EventSubscription state</param>
    /// <returns>Subscriptions in the state</returns>
    IReadOnlyList<IEventSubscription> GetByState(EventSubscriptionState state);

    /// <summary>
    /// Gets all subscriptions for a specific scope.
    /// </summary>
    /// <param name="scope">EventSubscription scope</param>
    /// <returns>Subscriptions for the scope</returns>
    IReadOnlyList<IEventSubscription> GetByScope(EventSubscriptionScope scope);

    #endregion

    #region Lifecycle Operations

    /// <summary>
    /// Activates a pending subscription (providers will be notified).
    /// </summary>
    /// <param name="subscriptionId">EventSubscription ID</param>
    Task ActivateAsync(EventSubscriptionId subscriptionId);

    /// <summary>
    /// Deactivates an active subscription without disposing it (providers will be notified).
    /// </summary>
    /// <param name="subscriptionId">EventSubscription ID</param>
    Task DeactivateAsync(EventSubscriptionId subscriptionId);

    /// <summary>
    /// Reactivates a deactivated subscription.
    /// </summary>
    /// <param name="subscriptionId">EventSubscription ID</param>
    Task ReactivateAsync(EventSubscriptionId subscriptionId);

    #endregion

    #region Batch Operations

    /// <summary>
    /// Removes and disposes all subscriptions for a specific event type.
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsubscribeByEventTypeAsync(Type eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and disposes all subscriptions for a specific handler type.
    /// </summary>
    /// <param name="handlerType">Handler type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsubscribeByHandlerTypeAsync(Type handlerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and disposes all subscriptions for a specific service key.
    /// </summary>
    /// <param name="serviceKey">Service key (null for default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsubscribeByServiceKeyAsync(string? serviceKey, CancellationToken cancellationToken = default);

    #endregion
}
