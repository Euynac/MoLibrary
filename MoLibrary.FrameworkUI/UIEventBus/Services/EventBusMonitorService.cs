using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Abstractions.Subscriptions;
using MoLibrary.EventBus.Constants;
using MoLibrary.EventBus.Models;
using MoLibrary.FrameworkUI.UIEventBus.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UIEventBus.Services;

/// <summary>
/// EventBus监控服务 - 封装订阅管理和实时更新功能
/// </summary>
public sealed class EventBusMonitorService(
    ISubscriptionManager subscriptionManager,
    IMoLocalEventBus localEventBus,
    IMoDistributedEventBus distributedEventBus,
    ILogger<EventBusMonitorService> logger) : IAsyncDisposable
{
    private readonly ISubscriptionManager _subscriptionManager = subscriptionManager;

    // 实时变更通道
    private readonly Channel<SubscriptionChangeViewModel> _changesChannel =
        Channel.CreateUnbounded<SubscriptionChangeViewModel>();

    // Observable订阅列表
    private readonly List<IDisposable> _observableSubscriptions = new();

    // 初始化标志
    private readonly object _initLock = new();
    private bool _initialized;

    #region Initialization

    /// <summary>
    /// 初始化订阅监听（延迟初始化）
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                // 订阅本地EventBus的变更
                var localSub = localEventBus.Subscriptions.Subscribe(
                    new SubscriptionChangeObserver(change => {
                        var vm = MapToChangeViewModel(change);
                        _changesChannel.Writer.TryWrite(vm);
                        logger.LogDebug("Local subscription change: {ChangeType} - {EventType}",
                            change.ChangeType, change.Subscription.EventType.Name);
                    }));
                _observableSubscriptions.Add(localSub);

                // 订阅分布式EventBus的变更
                var distSub = distributedEventBus.Subscriptions.Subscribe(
                    new SubscriptionChangeObserver(change => {
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
    /// Observable观察者实现
    /// </summary>
    private class SubscriptionChangeObserver(Action<SubscriptionChange> onNext) : IObserver<SubscriptionChange>
    {
        public void OnNext(SubscriptionChange value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// 获取所有订阅
    /// </summary>
    public async Task<Res<List<SubscriptionViewModel>>> GetAllSubscriptionsAsync()
    {
        try
        {
            var allSubscriptions = _subscriptionManager.GetAll().ToList();

            var allSubs = allSubscriptions
                .Select(MapToViewModel)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            var localCount = allSubscriptions.Count(s => s.Scope == SubscriptionScope.Local);
            var distCount = allSubscriptions.Count(s => s.Scope == SubscriptionScope.Distributed);

            logger.LogDebug("Retrieved {Count} subscriptions (Local: {LocalCount}, Distributed: {DistCount})",
                allSubs.Count, localCount, distCount);

            return Res.Ok(allSubs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all subscriptions");
            return Res.Fail($"获取订阅列表失败: {ex.Message}", ResponseCode.InternalError);
        }
    }

    /// <summary>
    /// 根据ID获取订阅
    /// </summary>
    public async Task<Res<SubscriptionViewModel?>> GetSubscriptionByIdAsync(SubscriptionId subscriptionId)
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
            logger.LogError(ex, "Failed to get subscription by ID: {SubscriptionId}", subscriptionId);
            return Res.Fail($"获取订阅详情失败: {ex.Message}", ResponseCode.InternalError);
        }
    }

    /// <summary>
    /// 过滤订阅
    /// </summary>
    public async Task<Res<List<SubscriptionViewModel>>> FilterSubscriptionsAsync(SubscriptionFilter filter)
    {
        try
        {
            var allSubs = _subscriptionManager.GetAll().AsQueryable();

            // 应用过滤条件
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
            return Res.Fail($"过滤订阅失败: {ex.Message}", ResponseCode.InternalError);
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public async Task<Res<SubscriptionStatistics>> GetStatisticsAsync()
    {
        try
        {
            var allSubs = _subscriptionManager.GetAll().ToList();

            var stats = new SubscriptionStatistics
            {
                TotalSubscriptions = allSubs.Count,
                ActiveSubscriptions = allSubs.Count(s => s.State == SubscriptionState.Active),
                InactiveSubscriptions = allSubs.Count(s => s.State == SubscriptionState.Inactive),
                PendingSubscriptions = allSubs.Count(s => s.State == SubscriptionState.Pending),
                DisposedSubscriptions = allSubs.Count(s => s.State == SubscriptionState.Disposed),
                LocalSubscriptions = allSubs.Count(s => s.Scope == SubscriptionScope.Local),
                DistributedSubscriptions = allSubs.Count(s => s.Scope == SubscriptionScope.Distributed),
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
    /// 激活订阅
    /// </summary>
    public async Task<Res> ActivateSubscriptionAsync(SubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail("订阅不存在");
            }

            if (subscription.State == SubscriptionState.Active)
            {
                return Res.Fail("订阅已经是活跃状态");
            }

            if (subscription.State == SubscriptionState.Disposed)
            {
                return Res.Fail("无法激活已释放的订阅");
            }

            // 根据范围选择对应的管理器
            var manager = subscription.Scope == SubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            if (subscription.State == SubscriptionState.Inactive)
            {
                await manager.ReactivateAsync(subscriptionId);
            }
            else
            {
                await manager.ActivateAsync(subscriptionId);
            }

            logger.LogInformation("Activated subscription: {SubscriptionId}", subscriptionId);
            return Res.Ok("订阅已激活");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to activate subscription: {SubscriptionId}", subscriptionId);
            return Res.Fail($"激活订阅失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 停用订阅
    /// </summary>
    public async Task<Res> DeactivateSubscriptionAsync(SubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail("订阅不存在");
            }

            if (subscription.State != SubscriptionState.Active)
            {
                return Res.Fail($"只能停用活跃订阅，当前状态: {subscription.State}");
            }

            // 根据范围选择对应的管理器
            var manager = subscription.Scope == SubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.DeactivateAsync(subscriptionId);

            logger.LogInformation("Deactivated subscription: {SubscriptionId}", subscriptionId);
            return Res.Ok("订阅已停用");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deactivate subscription: {SubscriptionId}", subscriptionId);
            return Res.Fail($"停用订阅失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 移除订阅
    /// </summary>
    public async Task<Res> UnsubscribeAsync(SubscriptionId subscriptionId)
    {
        try
        {
            var subscription = _subscriptionManager.GetById(subscriptionId);

            if (subscription == null)
            {
                return Res.Fail("订阅不存在");
            }

            if (subscription.State == SubscriptionState.Disposed)
            {
                return Res.Fail("订阅已经被移除");
            }

            // 根据范围选择对应的管理器
            var manager = subscription.Scope == SubscriptionScope.Local
                ? localEventBus.Subscriptions
                : distributedEventBus.Subscriptions;

            await manager.UnsubscribeAsync(subscriptionId);

            logger.LogInformation("Removed subscription: {SubscriptionId}", subscriptionId);
            return Res.Ok("订阅已移除");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unsubscribe: {SubscriptionId}", subscriptionId);
            return Res.Fail($"移除订阅失败: {ex.Message}");
        }
    }

    #endregion

    #region Real-time Subscription

    /// <summary>
    /// 订阅实时变更通知
    /// </summary>
    public ChannelReader<SubscriptionChangeViewModel> SubscribeToChanges()
    {
        EnsureInitialized();
        return _changesChannel.Reader;
    }

    #endregion

    #region Mapping Methods

    /// <summary>
    /// 将ISubscription映射为ViewModel
    /// </summary>
    private SubscriptionViewModel MapToViewModel(ISubscription subscription)
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

        // 提取 Action 处理器元数据
        if (subscription.HandlerType == null) // Action 处理器
        {
            vm.ActionMethodName = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodName);
            vm.ActionDeclaringType = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionDeclaringType);
            vm.ActionMethodSignature = subscription.GetMetadata<string>(SubscriptionMetadataKeys.ActionMethodSignature);
            vm.ActionIsStatic = subscription.GetMetadata<bool?>(SubscriptionMetadataKeys.ActionIsStatic);
        }

        return vm;
    }

    /// <summary>
    /// 将SubscriptionChange映射为ViewModel
    /// </summary>
    private SubscriptionChangeViewModel MapToChangeViewModel(SubscriptionChange change)
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

        // 取消所有Observable订阅
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

        // 关闭Channel
        _changesChannel.Writer.Complete();

        await Task.CompletedTask;
        logger.LogInformation("EventBusMonitorService disposed");
    }

    #endregion
}
