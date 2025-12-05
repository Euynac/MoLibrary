using MoLibrary.EventBus.Models;

namespace MoLibrary.EventBus.Abstractions.Subscriptions;

/// <summary>
/// Manages all event subscriptions with queryable, observable capabilities.
/// Serves as the single source of truth for subscription state.
/// </summary>
public interface ISubscriptionManager : IObservable<SubscriptionChange>
{
    #region Subscription CRUD

    /// <summary>
    /// Creates and activates a new subscription.
    /// </summary>
    /// <param name="descriptor">Subscription descriptor</param>
    /// <returns>The created and activated subscription</returns>
    Task<ISubscription> SubscribeAsync(SubscriptionDescriptor descriptor);

    /// <summary>
    /// Creates and activates multiple subscriptions in a single batch.
    /// </summary>
    /// <param name="descriptors">Collection of subscription descriptors</param>
    /// <returns>Created and activated subscriptions</returns>
    Task<IReadOnlyList<ISubscription>> SubscribeBatchAsync(IEnumerable<SubscriptionDescriptor> descriptors);

    /// <summary>
    /// Removes and disposes a subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    Task UnsubscribeAsync(SubscriptionId subscriptionId);

    /// <summary>
    /// Removes and disposes multiple subscriptions in a single batch.
    /// </summary>
    /// <param name="subscriptionIds">Collection of subscription IDs</param>
    Task UnsubscribeBatchAsync(IEnumerable<SubscriptionId> subscriptionIds);

    /// <summary>
    /// Removes and disposes all subscriptions matching a predicate.
    /// </summary>
    /// <param name="predicate">Filter predicate</param>
    Task UnsubscribeWhereAsync(Func<ISubscription, bool> predicate);

    #endregion

    #region Query Operations

    /// <summary>
    /// Gets all subscriptions as a queryable collection.
    /// </summary>
    /// <returns>Queryable subscription collection</returns>
    IQueryable<ISubscription> GetAll();

    /// <summary>
    /// Gets a specific subscription by ID.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <returns>Subscription if found, null otherwise</returns>
    ISubscription? GetById(SubscriptionId subscriptionId);

    /// <summary>
    /// Gets all subscriptions for a specific service key.
    /// </summary>
    /// <param name="serviceKey">Service key (null for default)</param>
    /// <returns>Subscriptions for the service key</returns>
    IReadOnlyList<ISubscription> GetByServiceKey(string? serviceKey);

    /// <summary>
    /// Gets all subscriptions for a specific topic name.
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <returns>Subscriptions for the topic</returns>
    IReadOnlyList<ISubscription> GetByTopicName(string topicName);

    /// <summary>
    /// Gets all subscriptions for a specific event type.
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <returns>Subscriptions for the event type</returns>
    IReadOnlyList<ISubscription> GetByEventType(Type eventType);

    /// <summary>
    /// Gets all subscriptions in a specific state.
    /// </summary>
    /// <param name="state">Subscription state</param>
    /// <returns>Subscriptions in the state</returns>
    IReadOnlyList<ISubscription> GetByState(SubscriptionState state);

    /// <summary>
    /// Gets all subscriptions for a specific scope.
    /// </summary>
    /// <param name="scope">Subscription scope</param>
    /// <returns>Subscriptions for the scope</returns>
    IReadOnlyList<ISubscription> GetByScope(SubscriptionScope scope);

    #endregion

    #region Lifecycle Operations

    /// <summary>
    /// Activates a pending subscription (providers will be notified).
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    Task ActivateAsync(SubscriptionId subscriptionId);

    /// <summary>
    /// Deactivates an active subscription without disposing it (providers will be notified).
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    Task DeactivateAsync(SubscriptionId subscriptionId);

    /// <summary>
    /// Reactivates a deactivated subscription.
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    Task ReactivateAsync(SubscriptionId subscriptionId);

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
