using System.Threading.Channels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Constants;
using Monica.EventBus.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Tool.Extensions;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// EventBus monitoring service - encapsulates subscription management and real-time update functions
/// </summary>
public sealed class EventBusMonitorService(
    IEventSubscriptionRegistry subscriptionManager,
    ITopicSubscriptionStatusStore topicStatusStore,
    ILocalEventBus localEventBus,
    IDistributedEventBus distributedEventBus,
    IStringLocalizer<EventBusResource> localizer,
    ILogger<EventBusMonitorService> logger) : IAsyncDisposable
{
    private readonly IEventSubscriptionRegistry _subscriptionManager = subscriptionManager;
    private readonly ITopicSubscriptionStatusStore _topicStatusStore = topicStatusStore;

    // Change channels in real time
    private readonly Channel<EventBusMonitorChange> _changesChannel =
        Channel.CreateUnbounded<EventBusMonitorChange>();

    // Observable subscription list
    private readonly List<IDisposable> _observableSubscriptions = new();

    // initialization flag
    private readonly object _initLock = new();
    private bool _initialized;

    #region Initialization

    /// <summary>
    /// Initialize subscription listening (lazy initialization)
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                // Subscribe to local EventBus changes
                var localSub = localEventBus.Subscriptions.Subscribe(
                    new SubscriptionChangeObserver(change => {
                        if (EventBusTestMetadataKeys.IsTestListenerSubscription(change.Subscription))
                        {
                            return;
                        }

                        _changesChannel.Writer.TryWrite(
                            new EventBusMonitorChange.SubscriptionChanged(MapToChangeViewModel(change)));
                        logger.LogDebug("Local subscription change: {ChangeType} - {EventType}",
                            change.ChangeType, change.Subscription.EventType.Name);
                    }));
                _observableSubscriptions.Add(localSub);

                // Subscribe to changes in distributed EventBus
                var distSub = distributedEventBus.Subscriptions.Subscribe(
                    new SubscriptionChangeObserver(change => {
                        if (EventBusTestMetadataKeys.IsTestListenerSubscription(change.Subscription))
                        {
                            return;
                        }

                        _changesChannel.Writer.TryWrite(
                            new EventBusMonitorChange.SubscriptionChanged(MapToChangeViewModel(change)));
                        logger.LogDebug("Distributed subscription change: {ChangeType} - {EventType}",
                            change.ChangeType, change.Subscription.EventType.Name);
                    }));
                _observableSubscriptions.Add(distSub);

                // Topic runtime-status changes also refresh the monitor views
                _topicStatusStore.StatusChanged += OnTopicStatusChanged;

                _initialized = true;
                logger.LogInformation("EventBusMonitorService initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize EventBusMonitorService");
                throw;
            }
        }
    }

    private void OnTopicStatusChanged(TopicSubscriptionStatus status)
    {
        _changesChannel.Writer.TryWrite(new EventBusMonitorChange.TopicStatusChanged(status));
    }

    /// <summary>
    /// Observable observer implementation
    /// </summary>
    private class SubscriptionChangeObserver(Action<EventSubscriptionChange> onNext) : IObserver<EventSubscriptionChange>
    {
        public void OnNext(EventSubscriptionChange value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Get all subscriptions
    /// </summary>
    public async Task<Res<List<SubscriptionViewModel>>> GetAllSubscriptionsAsync()
    {
        try
        {
            var allSubscriptions = GetVisibleSubscriptions();

            var allSubs = allSubscriptions
                .Select(MapToViewModel)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            var localCount = allSubscriptions.Count(s => s.Scope == EventSubscriptionScope.Local);
            var distCount = allSubscriptions.Count(s => s.Scope == EventSubscriptionScope.Distributed);

            logger.LogDebug("Retrieved {Count} subscriptions (Local: {LocalCount}, Distributed: {DistCount})",
                allSubs.Count, localCount, distCount);

            return Res.Ok(allSubs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all subscriptions");
            return Res.Fail(localizer["Services:Monitor:GetSubscriptionsFailed", ex.GetMessageRecursively()], ResStatus.InternalError);
        }
    }

    /// <summary>
    /// Get subscription based on ID
    /// </summary>
    public async Task<Res<SubscriptionViewModel?>> GetSubscriptionByIdAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Ok<SubscriptionViewModel?>(null);
            }

            var vm = MapToViewModel(subscription);
            return Res.Ok<SubscriptionViewModel?>(vm);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get subscription by ID: {EventSubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Monitor:GetSubscriptionDetailFailed", ex.GetMessageRecursively()], ResStatus.InternalError);
        }
    }

    /// <summary>
    /// Filter subscriptions
    /// </summary>
    public async Task<Res<List<SubscriptionViewModel>>> FilterSubscriptionsAsync(SubscriptionFilter filter)
    {
        try
        {
            var allSubs = GetVisibleSubscriptions().AsQueryable();

            // Apply filters
            if (filter.State.HasValue)
            {
                allSubs = allSubs.Where(s => s.State == filter.State.Value);
            }

            if (filter.Scope.HasValue)
            {
                allSubs = allSubs.Where(s => s.Scope == filter.Scope.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.ServiceKey))
            {
                allSubs = allSubs.Where(s => s.ServiceKey == filter.ServiceKey);
            }

            if (filter.IsAutoDiscovered.HasValue)
            {
                allSubs = allSubs.Where(s => s.IsAutoDiscovered == filter.IsAutoDiscovered.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var searchText = filter.SearchText.ToLowerInvariant();
                allSubs = allSubs.Where(s =>
                    s.EventType.Name.ToLowerInvariant().Contains(searchText) ||
                    s.TopicName.ToLowerInvariant().Contains(searchText));
            }

            var result = allSubs
                .ToList()
                .Select(MapToViewModel)
                .ToList();

            // Runtime-state filtering applies to the mapped view because the topic status join
            // happens during mapping.
            if (filter.TopicRuntimeState.HasValue)
            {
                result = result
                    .Where(s => s.TopicRuntimeState == filter.TopicRuntimeState.Value)
                    .ToList();
            }

            result = result.OrderByDescending(s => s.CreatedAt).ToList();

            logger.LogDebug("Filtered subscriptions: {Count} results", result.Count);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to filter subscriptions");
            return Res.Fail(localizer["Services:Monitor:FilterSubscriptionsFailed", ex.GetMessageRecursively()], ResStatus.InternalError);
        }
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public async Task<Res<SubscriptionStatistics>> GetStatisticsAsync()
    {
        try
        {
            var allSubs = GetVisibleSubscriptions();
            var viewModels = allSubs.Select(MapToViewModel).ToList();

            var stats = new SubscriptionStatistics
            {
                TotalSubscriptions = viewModels.Count,
                ActiveSubscriptions = viewModels.Count(s => s.State == EventSubscriptionState.Active),
                InactiveSubscriptions = viewModels.Count(s => s.State == EventSubscriptionState.Inactive),
                PendingSubscriptions = viewModels.Count(s => s.State == EventSubscriptionState.Pending),
                DisposedSubscriptions = viewModels.Count(s => s.State == EventSubscriptionState.Disposed),
                UnhealthySubscriptions = viewModels.Count(s => s.IsUnhealthy),
                LocalSubscriptions = viewModels.Count(s => s.Scope == EventSubscriptionScope.Local),
                DistributedSubscriptions = viewModels.Count(s => s.Scope == EventSubscriptionScope.Distributed),
                AutoDiscoveredCount = viewModels.Count(s => s.IsAutoDiscovered),
                ManualSubscriptionCount = viewModels.Count(s => !s.IsAutoDiscovered),
                ActionHandlerCount = viewModels.Count(s => s.HandlerType == null),
                TypeHandlerCount = viewModels.Count(s => s.HandlerType != null),
                TopEventTypes = viewModels
                    .GroupBy(s => s.EventType)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new EventTypeCount
                    {
                        EventType = g.Key,
                        EventTypeShortName = g.First().EventTypeShortName,
                        Count = g.Count()
                    })
                    .ToList(),
                TopTopics = viewModels
                    .GroupBy(s => s.TopicName)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new TopicCount
                    {
                        TopicName = g.Key,
                        Count = g.Count()
                    })
                    .ToList()
            };

            stats.UnhealthyTopics = _topicStatusStore.GetAll()
                .Count(t => t.State is TopicSubscriptionRuntimeState.Recovering or TopicSubscriptionRuntimeState.Failed);

            logger.LogDebug("Generated statistics: Total={Total}, Active={Active}, Unhealthy={Unhealthy}",
                stats.TotalSubscriptions, stats.ActiveSubscriptions, stats.UnhealthySubscriptions);

            return Res.Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate statistics");
            return Res.Fail(localizer["Services:Monitor:StatisticsFailed", ex.GetMessageRecursively()]);
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Activate subscription
    /// </summary>
    public async Task<Res> ActivateSubscriptionAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            if (subscription.State == EventSubscriptionState.Active)
            {
                return Res.Fail(localizer["Services:Monitor:AlreadyActive"]);
            }

            if (subscription.State == EventSubscriptionState.Disposed)
            {
                return Res.Fail(localizer["Services:Monitor:CannotActivateDisposed"]);
            }

            // Select the corresponding manager based on the scope
            var manager = subscription.Scope == EventSubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            if (subscription.State == EventSubscriptionState.Inactive)
            {
                await manager.ReactivateAsync(subscriptionId);
            }
            else
            {
                await manager.ActivateAsync(subscriptionId);
            }

            logger.LogInformation("Activated subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Ok(localizer["Services:Monitor:Activated"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to activate subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Monitor:ActivateFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Deactivate subscription
    /// </summary>
    public async Task<Res> DeactivateSubscriptionAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            if (subscription.State != EventSubscriptionState.Active)
            {
                return Res.Fail(localizer["Services:Monitor:OnlyActiveCanDeactivate", subscription.State]);
            }

            // Select the corresponding manager based on the scope
            var manager = subscription.Scope == EventSubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.DeactivateAsync(subscriptionId);

            logger.LogInformation("Deactivated subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Ok(localizer["Services:Monitor:Deactivated"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deactivate subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Monitor:DeactivateFailed", ex.GetMessageRecursively()]);
        }
    }

    /// <summary>
    /// Remove subscription
    /// </summary>
    public async Task<Res> UnsubscribeAsync(EventSubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail(localizer["Services:Common:SubscriptionNotFound"]);
            }

            if (subscription.State == EventSubscriptionState.Disposed)
            {
                return Res.Fail(localizer["Services:Monitor:AlreadyRemoved"]);
            }

            // Select the corresponding manager based on the scope
            var manager = subscription.Scope == EventSubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.UnsubscribeAsync(subscriptionId);

            logger.LogInformation("Removed subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Ok(localizer["Services:Monitor:Removed"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unsubscribe: {EventSubscriptionId}", subscriptionId);
            return Res.Fail(localizer["Services:Monitor:RemoveFailed", ex.GetMessageRecursively()]);
        }
    }

    #endregion

    #region Real-time Subscription

    /// <summary>
    /// Subscribe to real-time change notifications
    /// </summary>
    public ChannelReader<EventBusMonitorChange> SubscribeToChanges()
    {
        EnsureInitialized();
        return _changesChannel.Reader;
    }

    /// <summary>
    /// Get the runtime status snapshots of all known topic subscriptions.
    /// </summary>
    public Res<List<TopicSubscriptionStatus>> GetAllTopicStatuses()
    {
        try
        {
            return Res.Ok(_topicStatusStore.GetAll().ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get topic statuses");
            return Res.Fail(localizer["Services:Monitor:GetTopicStatusesFailed", ex.GetMessageRecursively()], ResStatus.InternalError);
        }
    }

    #endregion

    #region Mapping Methods

    /// <summary>
    /// Map IEventSubscription to ViewModel
    /// </summary>
    private SubscriptionViewModel MapToViewModel(IEventSubscription subscription)
    {
        var vm = new SubscriptionViewModel
        {
            SubscriptionId = subscription.Id.ToString(),
            EventType = subscription.EventType.GetCleanFullName(),
            EventTypeShortName = subscription.EventType.GetCleanName(),
            TopicName = subscription.TopicName,
            ServiceKey = subscription.ServiceKey,
            HandlerType = subscription.HandlerType?.GetCleanFullName(),
            HandlerTypeShortName = subscription.HandlerType?.GetCleanName(),
            HandlerFactoryType = subscription.HandlerFactory.GetType().Name,
            Scope = subscription.Scope,
            State = subscription.State,
            CreatedAt = subscription.CreatedAt,
            ActivatedAt = subscription.ActivatedAt,
            DeactivatedAt = subscription.DeactivatedAt,
            IsAutoDiscovered = subscription.IsAutoDiscovered,
            Metadata = subscription.Metadata
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? "null")
        };

        // Extract Action handler metadata
        if (subscription.HandlerType == null)
        {
            vm.ActionMethodName = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodName);
            vm.ActionDeclaringType = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionDeclaringType);
            vm.ActionMethodSignature = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodSignature);
            vm.ActionIsStatic = subscription.GetMetadata<bool?>(SubscriptionMetadataKeys.ActionIsStatic);
        }

        // Distributed subscriptions join the runtime health reported by their provider
        if (subscription.Scope == EventSubscriptionScope.Distributed)
        {
            vm.TopicStatus = _topicStatusStore.Get(subscription.ServiceKey, subscription.TopicName);
        }

        return vm;
    }

    private List<IEventSubscription> GetVisibleSubscriptions()
    {
        return _subscriptionManager.GetAll()
            .Where(subscription => !EventBusTestMetadataKeys.IsTestListenerSubscription(subscription))
            .ToList();
    }

    /// <summary>
    /// Map EventSubscriptionChange to ViewModel
    /// </summary>
    private SubscriptionChangeViewModel MapToChangeViewModel(EventSubscriptionChange change)
    {
        return new SubscriptionChangeViewModel
        {
            ChangeType = change.ChangeType,
            Subscription = MapToViewModel(change.Subscription),
            Timestamp = change.Timestamp
        };
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        logger.LogInformation("Disposing EventBusMonitorService...");

        // Cancel all Observable subscriptions
        foreach (var subscription in _observableSubscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disposing observable subscription");
            }
        }
        _observableSubscriptions.Clear();

        // Detach the topic status change handler
        _topicStatusStore.StatusChanged -= OnTopicStatusChanged;

        // CloseChannel
        _changesChannel.Writer.Complete();

        await Task.CompletedTask;
        logger.LogInformation("EventBusMonitorService disposed");
    }

    #endregion
}
