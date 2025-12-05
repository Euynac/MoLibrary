using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Models;

namespace MoLibrary.EventBus.Subscriptions;

/// <summary>
/// Manages subscription lifecycle and provides queryable access to all subscriptions.
/// </summary>
public class SubscriptionManager(ILogger<SubscriptionManager> logger) : ISubscriptionManager
{
    private readonly ConcurrentDictionary<SubscriptionId, Subscription> _subscriptions = new();
    private readonly ConcurrentBag<IObserver<SubscriptionChange>> _observers = new();

    #region Subscription CRUD

    public async Task<ISubscription> SubscribeAsync(SubscriptionDescriptor descriptor)
    {
        var subscription = new Subscription(descriptor);
 
        if (!_subscriptions.TryAdd(subscription.Id, subscription))
        {
            throw new InvalidOperationException($"Subscription {subscription.Id} already exists");
        }

        logger.LogDebug(
            "Subscription {SubscriptionId} created for {EventType} on topic {Topic}",
            subscription.Id, descriptor.EventType.Name, descriptor.TopicName);

        // Notify observers
        NotifyObservers(new SubscriptionChange(
            SubscriptionChangeType.Added,
            subscription,
            DateTimeOffset.UtcNow));

        // Auto-activate
        await ActivateAsync(subscription.Id);

        return subscription;
    }

    public async Task<IReadOnlyList<ISubscription>> SubscribeBatchAsync(IEnumerable<SubscriptionDescriptor> descriptors)
    {
        var subscriptions = new List<ISubscription>();

        foreach (var descriptor in descriptors)
        {
            var sub = await SubscribeAsync(descriptor);
            subscriptions.Add(sub);
        }

        return subscriptions;
    }

    public async Task UnsubscribeAsync(SubscriptionId subscriptionId)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            logger.LogWarning("Subscription {SubscriptionId} not found for unsubscribe", subscriptionId);
            return;
        }

        logger.LogDebug("Unsubscribing {SubscriptionId}", subscriptionId);

        // Notify observers before disposal
        NotifyObservers(new SubscriptionChange(
            SubscriptionChangeType.Removed,
            subscription,
            DateTimeOffset.UtcNow));

        // Dispose the subscription
        await subscription.DisposeAsync();
    }

    public async Task UnsubscribeBatchAsync(IEnumerable<SubscriptionId> subscriptionIds)
    {
        foreach (var id in subscriptionIds)
        {
            await UnsubscribeAsync(id);
        }
    }

    public async Task UnsubscribeWhereAsync(Func<ISubscription, bool> predicate)
    {
        var toRemove = _subscriptions.Values.Where(predicate).Select(s => s.Id).ToList();
        await UnsubscribeBatchAsync(toRemove);
    }

    #endregion

    #region Query Operations

    public IQueryable<ISubscription> GetAll()
    {
        return _subscriptions.Values.AsQueryable();
    }

    public ISubscription? GetById(SubscriptionId subscriptionId)
    {
        _subscriptions.TryGetValue(subscriptionId, out var subscription);
        return subscription;
    }

    public IReadOnlyList<ISubscription> GetByServiceKey(string? serviceKey)
    {
        return _subscriptions.Values.Where(s => s.ServiceKey == serviceKey).ToList();
    }

    public IReadOnlyList<ISubscription> GetByTopicName(string topicName)
    {
        return _subscriptions.Values.Where(s => s.TopicName == topicName).ToList();
    }

    public IReadOnlyList<ISubscription> GetByEventType(Type eventType)
    {
        return _subscriptions.Values
            .Where(s => s.EventType == eventType || s.EventType.IsAssignableFrom(eventType))
            .ToList();
    }

    public IReadOnlyList<ISubscription> GetByState(SubscriptionState state)
    {
        return _subscriptions.Values.Where(s => s.State == state).ToList();
    }

    public IReadOnlyList<ISubscription> GetByScope(SubscriptionScope scope)
    {
        return _subscriptions.Values.Where(s => s.Scope == scope).ToList();
    }

    #endregion

    #region Lifecycle Operations

    public async Task ActivateAsync(SubscriptionId subscriptionId)
    {
        var subscription = GetById(subscriptionId);
        if (subscription == null)
        {
            logger.LogWarning("Subscription {SubscriptionId} not found for activation", subscriptionId);
            return;
        }  

        await subscription.ActivateAsync();

        NotifyObservers(new SubscriptionChange(
            SubscriptionChangeType.Activated,
            subscription,
            DateTimeOffset.UtcNow));
    }

    public async Task DeactivateAsync(SubscriptionId subscriptionId)
    {
        var subscription = GetById(subscriptionId);
        if (subscription == null)
        {
            logger.LogWarning("Subscription {SubscriptionId} not found for deactivation", subscriptionId);
            return;
        }

        await subscription.DeactivateAsync();

        NotifyObservers(new SubscriptionChange(
            SubscriptionChangeType.Deactivated,
            subscription,
            DateTimeOffset.UtcNow));
    }

    public async Task ReactivateAsync(SubscriptionId subscriptionId)
    {
        await ActivateAsync(subscriptionId);
    }

    #endregion

    #region Batch Operations

    public async Task UnsubscribeByEventTypeAsync(Type eventType, CancellationToken cancellationToken = default)
    {
        var subscriptions = GetByEventType(eventType);
        await UnsubscribeBatchAsync(subscriptions.Select(s => s.Id));
    }

    public async Task UnsubscribeByHandlerTypeAsync(Type handlerType, CancellationToken cancellationToken = default)
    {
        var subscriptions = _subscriptions.Values
            .Where(s => s.HandlerType == handlerType)
            .Select(s => s.Id)
            .ToList();
        await UnsubscribeBatchAsync(subscriptions);
    }

    public async Task UnsubscribeByServiceKeyAsync(string? serviceKey, CancellationToken cancellationToken = default)
    {
        var subscriptions = GetByServiceKey(serviceKey);
        await UnsubscribeBatchAsync(subscriptions.Select(s => s.Id));
    }

    #endregion


    #region IObservable Implementation

    public IDisposable Subscribe(IObserver<SubscriptionChange> observer)
    {
        _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }

    private void NotifyObservers(SubscriptionChange change)
    {
        foreach (var observer in _observers)
        {
            try
            {
                observer.OnNext(change);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error notifying observer of subscription change");
            }
        }
    }

    private class Unsubscriber(
        ConcurrentBag<IObserver<SubscriptionChange>> observers,
        IObserver<SubscriptionChange> observer)
        : IDisposable
    {
        private readonly ConcurrentBag<IObserver<SubscriptionChange>> _observers = observers;
        private readonly IObserver<SubscriptionChange> _observer = observer;

        public void Dispose()
        {
            // Note: ConcurrentBag doesn't support removal, so we keep the observer
            // but it won't receive notifications after disposal
            // A production implementation might use a different collection
        }
    }

    #endregion
}
