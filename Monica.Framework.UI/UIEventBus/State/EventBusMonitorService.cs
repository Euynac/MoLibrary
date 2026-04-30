using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Monica.Core.Results;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Constants;
using Monica.EventBus.Models;
using Monica.Framework.UI.UIEventBus.Models;
using Monica.Tool.Extensions;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// EventBus monitoring service - encapsulates subscription management and real-time update functions
/// </summary>
public sealed class EventBusMonitorService(
    IEventSubscriptionRegistry subscriptionManager,
    ILocalEventBus localEventBus,
    IDistributedEventBus distributedEventBus,
    ILogger<EventBusMonitorService> logger) : IAsyncDisposable
{
    private readonly IEventSubscriptionRegistry _subscriptionManager = subscriptionManager;

    // Change channels in real time
    private readonly Channel<SubscriptionChangeViewModel> _changesChannel =
        Channel.CreateUnbounded<SubscriptionChangeViewModel>();

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

                        var vm = MapToChangeViewModel(change);
                        _changesChannel.Writer.TryWrite(vm);
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

                        var vm = MapToChangeViewModel(change);
                        _changesChannel.Writer.TryWrite(vm);
                        logger.LogDebug("Distributed subscription change: {ChangeType} - {EventType}",
                            change.ChangeType, change.Subscription.EventType.Name);
                    }));
                _observableSubscriptions.Add(distSub);

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
            return Res.Fail($"获取订阅列表失败: {ex.Message}", ResStatus.InternalError);
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
            return Res.Fail($"获取订阅详情失败: {ex.Message}", ResStatus.InternalError);
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
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            logger.LogDebug("Filtered subscriptions: {Count} results", result.Count);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to filter subscriptions");
            return Res.Fail($"过滤订阅失败: {ex.Message}", ResStatus.InternalError);
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

            var stats = new SubscriptionStatistics
            {
                TotalSubscriptions = allSubs.Count,
                ActiveSubscriptions = allSubs.Count(s => s.State == EventSubscriptionState.Active),
                InactiveSubscriptions = allSubs.Count(s => s.State == EventSubscriptionState.Inactive),
                PendingSubscriptions = allSubs.Count(s => s.State == EventSubscriptionState.Pending),
                DisposedSubscriptions = allSubs.Count(s => s.State == EventSubscriptionState.Disposed),
                LocalSubscriptions = allSubs.Count(s => s.Scope == EventSubscriptionScope.Local),
                DistributedSubscriptions = allSubs.Count(s => s.Scope == EventSubscriptionScope.Distributed),
                AutoDiscoveredCount = allSubs.Count(s => s.IsAutoDiscovered),
                ManualSubscriptionCount = allSubs.Count(s => !s.IsAutoDiscovered),
                ActionHandlerCount = allSubs.Count(s => s.HandlerType == null),
                TypeHandlerCount = allSubs.Count(s => s.HandlerType != null),
                TopEventTypes = allSubs
                    .GroupBy(s => s.EventType)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new EventTypeCount
                    {
                        EventType = g.Key.GetCleanFullName(),
                        EventTypeShortName = g.Key.GetCleanName(),
                        Count = g.Count()
                    })
                    .ToList(),
                TopTopics = allSubs
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

            logger.LogDebug("Generated statistics: Total={Total}, Active={Active}",
                stats.TotalSubscriptions, stats.ActiveSubscriptions);

            return Res.Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate statistics");
            return Res.Fail($"统计失败: {ex.Message}");
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
                return Res.Fail("订阅不存在");
            }

            if (subscription.State == EventSubscriptionState.Active)
            {
                return Res.Fail("订阅已经是活跃状态");
            }

            if (subscription.State == EventSubscriptionState.Disposed)
            {
                return Res.Fail("无法激活已释放的订阅");
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
            return Res.Ok("订阅已激活");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to activate subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Fail($"激活订阅失败: {ex.Message}");
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
                return Res.Fail("订阅不存在");
            }

            if (subscription.State != EventSubscriptionState.Active)
            {
                return Res.Fail($"只能停用活跃订阅，当前状态: {subscription.State}");
            }

            // Select the corresponding manager based on the scope
            var manager = subscription.Scope == EventSubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.DeactivateAsync(subscriptionId);

            logger.LogInformation("Deactivated subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Ok("订阅已停用");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deactivate subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Fail($"停用订阅失败: {ex.Message}");
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
                return Res.Fail("订阅不存在");
            }

            if (subscription.State == EventSubscriptionState.Disposed)
            {
                return Res.Fail("订阅已经被移除");
            }

            // Select the corresponding manager based on the scope
            var manager = subscription.Scope == EventSubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.UnsubscribeAsync(subscriptionId);

            logger.LogInformation("Removed subscription: {EventSubscriptionId}", subscriptionId);
            return Res.Ok("订阅已移除");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unsubscribe: {EventSubscriptionId}", subscriptionId);
            return Res.Fail($"移除订阅失败: {ex.Message}");
        }
    }

    #endregion

    #region Real-time Subscription

    /// <summary>
    /// Subscribe to real-time change notifications
    /// </summary>
    public ChannelReader<SubscriptionChangeViewModel> SubscribeToChanges()
    {
        EnsureInitialized();
        return _changesChannel.Reader;
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
        if (subscription.HandlerType == null) // Action 处理器
        {
            vm.ActionMethodName = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodName);
            vm.ActionDeclaringType = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionDeclaringType);
            vm.ActionMethodSignature = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodSignature);
            vm.ActionIsStatic = subscription.GetMetadata<bool?>(SubscriptionMetadataKeys.ActionIsStatic);
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

        // CloseChannel
        _changesChannel.Writer.Complete();

        await Task.CompletedTask;
        logger.LogInformation("EventBusMonitorService disposed");
    }

    #endregion
}
