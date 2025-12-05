using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Models;

namespace MoLibrary.EventBus.Services;

/// <summary>
/// Abstract base class for subscription hosted services.
/// Listens to SubscriptionManager changes and manages external subscriptions (e.g., Dapr, RabbitMQ).
/// Each derived class handles a specific ServiceKey and implements the actual subscription management.
/// </summary>
public abstract class EventBusSubscriptionHostedServiceBase(
    ISubscriptionManager subscriptionManager,
    IMoEventBus eventBus,
    ILogger logger,
    string? serviceKey)
    : IHostedService, IObserver<SubscriptionChange>
{
    protected readonly ISubscriptionManager SubscriptionManager = subscriptionManager;
    protected readonly IMoEventBus EventBus = eventBus;
    protected readonly ILogger Logger = logger;
    protected readonly string? ServiceKey = serviceKey;

    /// <summary>
    /// Tracks active external subscriptions by SubscriptionId.
    /// Derived classes manage this dictionary to track their external subscriptions.
    /// </summary>
    protected readonly ConcurrentDictionary<SubscriptionId, IAsyncDisposable> ExternalSubscriptions = new();

    private IDisposable? _subscriptionManagerObserver;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to SubscriptionManager changes
        _subscriptionManagerObserver = SubscriptionManager.Subscribe(this);

        // Create subscriptions for existing active distributed subscriptions matching our ServiceKey
        var existingSubscriptions = SubscriptionManager.GetAll().AsEnumerable()
            .Where(ShouldHandleSubscription)
            .Where(s => s.State == SubscriptionState.Active)
            .ToList();

        foreach (var subscription in existingSubscriptions)
        {
            await CreateExternalSubscriptionAsync(subscription, cancellationToken);
        }

        Logger.LogInformation(
            "{ServiceName} started for ServiceKey '{ServiceKey}' with {Count} existing subscriptions",
            GetType().Name,
            ServiceKey ?? "default",
            existingSubscriptions.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from SubscriptionManager
        _subscriptionManagerObserver?.Dispose();

        // Cancel all external subscriptions
        var tasks = ExternalSubscriptions.Values.Select(d => d.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);
        ExternalSubscriptions.Clear();

        Logger.LogInformation(
            "{ServiceName} stopped for ServiceKey '{ServiceKey}'",
            GetType().Name,
            ServiceKey ?? "default");
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
                    CreateExternalSubscriptionAsync(change.Subscription, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    break;

                case SubscriptionChangeType.Removed:
                case SubscriptionChangeType.Deactivated:
                    RemoveExternalSubscriptionAsync(change.Subscription.Id)
                        .GetAwaiter().GetResult();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error handling subscription change {ChangeType} for {SubscriptionId}",
                change.ChangeType, change.Subscription.Id);
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
    /// Creates an external subscription for the given subscription.
    /// Implemented by derived classes (e.g., Dapr streaming subscription).
    /// </summary>
    protected abstract Task CreateExternalSubscriptionAsync(ISubscription subscription, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an external subscription by its subscription ID.
    /// Default implementation disposes the tracked subscription.
    /// </summary>
    protected virtual async Task RemoveExternalSubscriptionAsync(SubscriptionId subscriptionId)
    {
        if (ExternalSubscriptions.TryRemove(subscriptionId, out var externalSubscription))
        {
            try
            {
                await externalSubscription.DisposeAsync();
                Logger.LogInformation(
                    "Removed external subscription {SubscriptionId} for ServiceKey '{ServiceKey}'",
                    subscriptionId,
                    ServiceKey ?? "default");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error disposing external subscription {SubscriptionId}",
                    subscriptionId);
            }
        }
    }
}
