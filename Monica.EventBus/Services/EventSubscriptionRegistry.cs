using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.EventBus.Models.Internal;

namespace Monica.EventBus.Services;

/// <summary>
/// Manages subscription lifecycle and provides queryable access to all subscriptions.
/// </summary>
public class EventSubscriptionRegistry(ILogger<EventSubscriptionRegistry> logger) : IEventSubscriptionRegistry
{
    private readonly ConcurrentDictionary<EventSubscriptionId, EventSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, IObserver<EventSubscriptionChange>> _observers = new();

    #region EventSubscription CRUD

    public async Task<IEventSubscription> SubscribeAsync(EventSubscriptionDescriptor descriptor)
    {
        var subscription = new EventSubscription(descriptor);
 
        if (!_subscriptions.TryAdd(subscription.Id, subscription))
        {
            throw new InvalidOperationException($"EventSubscription {subscription.Id} already exists");
        }

        logger.LogDebug(
            "EventSubscription {EventSubscriptionId} created for {EventType} on topic {Topic}",
            subscription.Id, descriptor.EventType.Name, descriptor.TopicName);

        // Notify observers
        NotifyObservers(new EventSubscriptionChange(
            EventSubscriptionChangeType.Added,
            subscription,
            DateTime.UtcNow));

        // Auto-activate
        await ActivateAsync(subscription.Id);

        return subscription;
    }

    public async Task<IReadOnlyList<IEventSubscription>> SubscribeBatchAsync(IEnumerable<EventSubscriptionDescriptor> descriptors)
    {
        var subscriptions = new List<IEventSubscription>();

        foreach (var descriptor in descriptors)
        {
            var sub = await SubscribeAsync(descriptor);
            subscriptions.Add(sub);
        }

        return subscriptions;
    }

    public async Task UnsubscribeAsync(EventSubscriptionId subscriptionId)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            logger.LogWarning("EventSubscription {EventSubscriptionId} not found for unsubscribe", subscriptionId);
            return;
        }

        logger.LogDebug("Unsubscribing {EventSubscriptionId}", subscriptionId);

        // Notify observers before disposal
        NotifyObservers(new EventSubscriptionChange(
            EventSubscriptionChangeType.Removed,
            subscription,
            DateTime.UtcNow));

        // Dispose the subscription
        await subscription.DisposeAsync();
    }

    public async Task UnsubscribeBatchAsync(IEnumerable<EventSubscriptionId> subscriptionIds)
    {
        foreach (var id in subscriptionIds)
        {
            await UnsubscribeAsync(id);
        }
    }

    public async Task UnsubscribeWhereAsync(Func<IEventSubscription, bool> predicate)
    {
        var toRemove = _subscriptions.Values.Where(predicate).Select(s => s.Id).ToList();
        await UnsubscribeBatchAsync(toRemove);
    }

    #endregion

    #region Query Operations

    public IQueryable<IEventSubscription> GetAll()
    {
        return _subscriptions.Values.AsQueryable();
    }

    public IEventSubscription? GetById(EventSubscriptionId subscriptionId)
    {
        _subscriptions.TryGetValue(subscriptionId, out var subscription);
        return subscription;
    }

    public IReadOnlyList<IEventSubscription> GetByServiceKey(string? serviceKey)
    {
        return _subscriptions.Values.Where(s => s.ServiceKey == serviceKey).ToList();
    }

    public IReadOnlyList<IEventSubscription> GetByTopicName(string topicName)
    {
        return _subscriptions.Values.Where(s => s.TopicName == topicName).ToList();
    }

    public IReadOnlyList<IEventSubscription> GetByEventType(Type eventType)
    {
        return _subscriptions.Values
            .Where(s => s.EventType == eventType || s.EventType.IsAssignableFrom(eventType))
            .ToList();
    }

    public IReadOnlyList<IEventSubscription> GetByState(EventSubscriptionState state)
    {
        return _subscriptions.Values.Where(s => s.State == state).ToList();
    }

    public IReadOnlyList<IEventSubscription> GetByScope(EventSubscriptionScope scope)
    {
        return _subscriptions.Values.Where(s => s.Scope == scope).ToList();
    }

    #endregion

    #region Lifecycle Operations

    public async Task ActivateAsync(EventSubscriptionId subscriptionId)
    {
        var subscription = GetById(subscriptionId);
        if (subscription == null)
        {
            logger.LogWarning("EventSubscription {EventSubscriptionId} not found for activation", subscriptionId);
            return;
        }  

        await subscription.ActivateAsync();

        NotifyObservers(new EventSubscriptionChange(
            EventSubscriptionChangeType.Activated,
            subscription,
            DateTime.UtcNow));
    }

    public async Task DeactivateAsync(EventSubscriptionId subscriptionId)
    {
        var subscription = GetById(subscriptionId);
        if (subscription == null)
        {
            logger.LogWarning("EventSubscription {EventSubscriptionId} not found for deactivation", subscriptionId);
            return;
        }

        await subscription.DeactivateAsync();

        NotifyObservers(new EventSubscriptionChange(
            EventSubscriptionChangeType.Deactivated,
            subscription,
            DateTime.UtcNow));
    }

    public async Task ReactivateAsync(EventSubscriptionId subscriptionId)
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

    public IDisposable Subscribe(IObserver<EventSubscriptionChange> observer)
    {
        var observerId = Guid.NewGuid();
        _observers[observerId] = observer;
        return new Unsubscriber(_observers, observerId);
    }

    private void NotifyObservers(EventSubscriptionChange change)
    {
        foreach (var observer in _observers.Values)
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

    private sealed class Unsubscriber(
        ConcurrentDictionary<Guid, IObserver<EventSubscriptionChange>> observers,
        Guid observerId)
        : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, IObserver<EventSubscriptionChange>> _observers = observers;
        private readonly Guid _observerId = observerId;

        public void Dispose()
        {
            _observers.TryRemove(_observerId, out _);
        }
    }

    #endregion
}
