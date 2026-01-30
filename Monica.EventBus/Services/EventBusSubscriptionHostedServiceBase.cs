using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Features.HostedServices;
using Monica.Core.Features.HostedServices.Models;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.Modules;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Abstractions.Subscriptions;
using Monica.EventBus.Models;

namespace Monica.EventBus.Services;

/// <summary>
/// Abstract base class for subscription hosted services.
/// Listens to SubscriptionManager changes and manages external subscriptions (e.g., Dapr, RabbitMQ).
/// Each derived class handles a specific ServiceKey and implements the actual subscription management.
/// Now includes observable state management for monitoring.
/// Runs as a background service that doesn't block application startup.
/// </summary>
public abstract class EventBusSubscriptionHostedServiceBase(
    ISubscriptionManager subscriptionManager,
    IMoEventBus eventBus,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger logger,
    string? serviceKey)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IObserver<SubscriptionChange>
{
    protected readonly ISubscriptionManager SubscriptionManager = subscriptionManager;
    protected readonly IMoEventBus EventBus = eventBus;
    protected readonly string? ServiceKey = serviceKey;

    /// <summary>
    /// Tracks active external subscriptions by SubscriptionId.
    /// Derived classes manage this dictionary to track their external subscriptions.
    /// </summary>
    protected readonly ConcurrentDictionary<SubscriptionId, IAsyncDisposable> ExternalSubscriptions = new();

    /// <summary>
    /// Tracks subscription information per topic.
    /// </summary>
    protected class TopicSubscriptionInfo
    {
        public required Type EventType { get; init; }
        public required HashSet<SubscriptionId> SubscriptionIds { get; init; }
        public IAsyncDisposable? ExternalSubscription { get; set; }
    }

    /// <summary>
    /// Tracks active topics and their associated subscriptions.
    /// Maps TopicName -> subscription metadata and external subscription.
    /// </summary>
    private readonly ConcurrentDictionary<string, TopicSubscriptionInfo> _topicSubscriptions = new();
    private readonly object _topicLock = new();

    private IDisposable? _subscriptionManagerObserver;

    /// <summary>
    /// Executes the background work for the subscription service.
    /// Initializes subscriptions, subscribes to changes, and keeps running until cancellation.
    /// </summary>
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("Initializing subscription manager observer", HostedServiceState.Starting);

        // Subscribe to SubscriptionManager changes
        _subscriptionManagerObserver = SubscriptionManager.Subscribe(this);

        // Group existing active subscriptions by topic
        var existingSubscriptions = SubscriptionManager.GetAll().AsEnumerable()
            .Where(ShouldHandleSubscription)
            .Where(s => s.State == SubscriptionState.Active)
            .ToList();

        RecordState($"Creating subscriptions for {existingSubscriptions.Count} existing subscriptions across topics", HostedServiceState.Starting);

        // Add subscriptions using the new logic (which handles topic grouping)
        foreach (var subscription in existingSubscriptions)
        {
            await HandleSubscriptionAddedAsync(subscription, stoppingToken);
        }

        Logger.LogInformation(
            "{ServiceName} initialized for ServiceKey '{ServiceKey}' with {SubscriptionCount} subscriptions across {TopicCount} topics",
            ServiceName,
            ServiceKey ?? "default",
            existingSubscriptions.Count,
            _topicSubscriptions.Count);

        RecordState("Background service running, listening for subscription changes", HostedServiceState.Running);

        // Keep the service running until cancellation is requested
        // The observer pattern handles subscription changes asynchronously
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
            Logger.LogDebug("{ServiceName} background task cancelled", ServiceName);
        }
    }

    /// <summary>
    /// Called during service shutdown. Disposes all external subscriptions and cleans up.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        RecordState("Disposing subscription manager observer", HostedServiceState.Stopping);

        // Unsubscribe from SubscriptionManager
        _subscriptionManagerObserver?.Dispose();

        RecordState($"Disposing {_topicSubscriptions.Count} external subscriptions", HostedServiceState.Stopping);

        // Dispose all external subscriptions
        var disposeTasks = _topicSubscriptions.Values
            .Where(info => info.ExternalSubscription != null)
            .Select(info => info.ExternalSubscription!.DisposeAsync().AsTask());

        await Task.WhenAll(disposeTasks);

        _topicSubscriptions.Clear();
        ExternalSubscriptions.Clear();

        Logger.LogInformation(
            "{ServiceName} cleanup completed for ServiceKey '{ServiceKey}'",
            ServiceName,
            ServiceKey ?? "default");

        // Call base to complete the shutdown
        await base.StopAsync(cancellationToken);
    }

    #region IObserver Implementation

    public void OnNext(SubscriptionChange change)
    {
        // Filter: only handle subscriptions matching our ServiceKey and criteria
        if (!ShouldHandleSubscription(change.Subscription))
        {
            return;
        }

        try
        {
            switch (change.ChangeType)
            {
                case SubscriptionChangeType.Added:
                case SubscriptionChangeType.Activated:
                    HandleSubscriptionAddedAsync(change.Subscription, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    break;

                case SubscriptionChangeType.Removed:
                case SubscriptionChangeType.Deactivated:
                    HandleSubscriptionRemovedAsync(change.Subscription.Id, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    break;
            }
        }
        catch (Exception ex)
        {
            RecordState($"Error handling subscription change {change.ChangeType} for {change.Subscription.Id}",
                HostedServiceState.Degraded, ex);

            Logger.LogError(ex,
                "Error handling subscription change {ChangeType} for {SubscriptionId}",
                change.ChangeType, change.Subscription.Id);
        }
    }

    private async Task HandleSubscriptionAddedAsync(ISubscription subscription, CancellationToken cancellationToken)
    {
        var topicName = subscription.TopicName;
        var eventType = subscription.EventType;
        bool shouldCreateExternal = false;

        try
        {
            lock (_topicLock)
            {
                if (_topicSubscriptions.TryGetValue(topicName, out var topicInfo))
                {
                    // Topic already exists - validate same event type
                    if (topicInfo.EventType != eventType)
                    {
                        var errorMsg = $"Cannot add subscription {subscription.Id} for topic '{topicName}': " +
                                       $"Topic already has subscriptions with EventType '{topicInfo.EventType.Name}', " +
                                       $"but new subscription uses '{eventType.Name}'. " +
                                       $"Multiple event types per topic are not supported.";
                        RecordState(errorMsg, HostedServiceState.Faulted);
                        throw new InvalidOperationException(errorMsg);
                    }

                    // Add to existing topic's reference set
                    if (!topicInfo.SubscriptionIds.Add(subscription.Id))
                    {
                        Logger.LogWarning(
                            "Subscription {SubscriptionId} already registered for topic {Topic}",
                            subscription.Id, topicName);
                        return;
                    }

                    Logger.LogDebug(
                        "Added subscription {SubscriptionId} to existing topic {Topic} (total: {Count} subscriptions)",
                        subscription.Id, topicName, topicInfo.SubscriptionIds.Count);
                }
                else
                {
                    // New topic - create tracking info
                    var newTopicInfo = new TopicSubscriptionInfo
                    {
                        EventType = eventType,
                        SubscriptionIds = [subscription.Id]
                    };
                    _topicSubscriptions[topicName] = newTopicInfo;
                    shouldCreateExternal = true;

                    Logger.LogDebug(
                        "Created new topic {Topic} for subscription {SubscriptionId}",
                        topicName, subscription.Id);
                }
            }

            // Create external subscription if this is the first subscription for the topic
            if (shouldCreateExternal)
            {
                RecordState($"Creating external subscription for topic {topicName}", HostedServiceState.Running);

                await CreateExternalSubscriptionForTopicAsync(topicName, eventType, cancellationToken);

                RecordState($"Successfully created external subscription for topic {topicName}", HostedServiceState.Running);
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            RecordState($"Error adding subscription for topic {topicName}",
                HostedServiceState.Degraded, ex);
            throw;
        }
    }

    private async Task HandleSubscriptionRemovedAsync(SubscriptionId subscriptionId, CancellationToken cancellationToken)
    {
        // Find which topic this subscription belongs to
        string? topicToRemove = null;
        bool shouldRemoveExternal = false;

        try
        {
            lock (_topicLock)
            {
                foreach (var (topicName, topicInfo) in _topicSubscriptions)
                {
                    if (topicInfo.SubscriptionIds.Remove(subscriptionId))
                    {
                        Logger.LogDebug(
                            "Removed subscription {SubscriptionId} from topic {Topic} (remaining: {Count} subscriptions)",
                            subscriptionId, topicName, topicInfo.SubscriptionIds.Count);

                        // If this was the last subscription for the topic, mark for removal
                        if (topicInfo.SubscriptionIds.Count == 0)
                        {
                            topicToRemove = topicName;
                            shouldRemoveExternal = true;
                        }
                        break;
                    }
                }

                if (topicToRemove != null)
                {
                    _topicSubscriptions.TryRemove(topicToRemove, out _);
                }
            }

            // Remove external subscription if this was the last subscription for the topic
            if (shouldRemoveExternal && topicToRemove != null)
            {
                RecordState($"Removing external subscription for topic {topicToRemove}", HostedServiceState.Running);

                await RemoveExternalSubscriptionForTopicAsync(topicToRemove, cancellationToken);

                RecordState($"Successfully removed external subscription for topic {topicToRemove}", HostedServiceState.Running);
            }
        }
        catch (Exception ex)
        {
            RecordState($"Error removing subscription {subscriptionId}",
                HostedServiceState.Degraded, ex);
            throw;
        }
    }

    public void OnError(Exception error)
    {
        Logger.LogError(error, "Error in SubscriptionChange observable");
    }

    public void OnCompleted()
    {
        Logger.LogInformation("SubscriptionChange observable completed");
    }

    #endregion

    /// <summary>
    /// Determines if this hosted service should handle the given subscription.
    /// Override this method to add additional filtering criteria.
    /// </summary>
    protected virtual bool ShouldHandleSubscription(ISubscription subscription)
    {
        // Only handle subscriptions matching our ServiceKey and Distributed scope
        return subscription.ServiceKey == ServiceKey &&
               subscription.Scope == SubscriptionScope.Distributed;
    }

    /// <summary>
    /// Creates an external subscription for the given topic and event type.
    /// Called when the first subscription for a topic is added.
    /// </summary>
    /// <param name="topicName">The topic name to subscribe to</param>
    /// <param name="eventType">The event type for message deserialization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task CreateExternalSubscriptionForTopicAsync(
        string topicName,
        Type eventType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes an external subscription for the given topic.
    /// Called when the last subscription for a topic is removed.
    /// </summary>
    /// <param name="topicName">The topic name to unsubscribe from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task RemoveExternalSubscriptionForTopicAsync(
        string topicName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles an external message received from the messaging system.
    /// Triggers all registered handlers for the topic with the provided event data.
    /// </summary>
    /// <param name="topicName">The topic the message was received on</param>
    /// <param name="eventData">The deserialized event data object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task HandleExternalMessageAsync(
        string topicName,
        object eventData,
        CancellationToken cancellationToken)
    {
        // Get topic information
        if (!_topicSubscriptions.TryGetValue(topicName, out var topicInfo))
        {
            Logger.LogWarning(
                "Received message for untracked topic {Topic}, ignoring",
                topicName);
            return;
        }

        try
        {
            if (eventData != null)
            {
                // Trigger all handlers for this topic
                await ((EventBusBase)EventBus).TriggerHandlersAsync(
                    topicInfo.EventType,
                    eventData,
                    topicName,
                    cancellationToken);
            }
            else
            {
                RecordState($"Received null event data for topic {topicName}", HostedServiceState.Degraded);

                Logger.LogWarning(
                    "Event data for topic {Topic} was null",
                    topicName);
            }
        }
        catch (Exception ex)
        {
            RecordState($"Error handling external message for topic {topicName}",
                HostedServiceState.Degraded, ex);

            Logger.LogError(ex,
                "Error handling external message for topic {Topic}",
                topicName);
            throw;
        }
    }
}
