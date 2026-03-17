using Microsoft.Extensions.Logging;
using Monica.Core.Features.ObservableInstance;
using Monica.Framework.UI.UIObservableInstance.Models;
using Monica.Tool.Extensions;
using Monica.Tool.MoResponse;

namespace Monica.Framework.UI.UIObservableInstance.Services;

/// <summary>
/// ObservableInstance monitoring service - encapsulates instance query and management functionality
/// </summary>
public sealed class ObservableInstanceMonitorService(
    IObservableInstanceManager observableInstanceManager,
    ILogger<ObservableInstanceMonitorService> logger)
{
    private readonly IObservableInstanceManager _manager = observableInstanceManager;

    #region Query Methods

    /// <summary>
    /// Gets all registered observable instances
    /// </summary>
    public async Task<Res<List<ObservableInstanceViewModel>>> GetAllInstancesAsync()
    {
        try
        {
            var instances = _manager.GetAllInstances();

            var viewModels = instances
                .Select(MapToViewModel)
                .OrderByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Retrieved {Count} observable instances", viewModels.Count);

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all instances");
            return Res.Fail($"获取实例列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets an observable instance by instance ID
    /// </summary>
    public async Task<Res<ObservableInstanceViewModel?>> GetInstanceByIdAsync(string instanceId)
    {
        try
        {
            var instance = _manager.GetInstance(instanceId);

            if (instance == null)
            {
                return Res.Ok<ObservableInstanceViewModel?>(null);
            }

            var viewModel = MapToViewModel(instance);
            return Res.Ok<ObservableInstanceViewModel?>(viewModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instance by ID: {InstanceId}", instanceId);
            return Res.Fail($"获取实例详情失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all instances by type
    /// </summary>
    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesByTypeAsync(Type type)
    {
        try
        {
            var instances = _manager.GetInstancesByType(type);

            var viewModels = instances
                .Select(MapToViewModel)
                .OrderByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Retrieved {Count} instances of type {Type}", viewModels.Count, type.GetCleanName());

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances by type: {Type}", type.GetCleanName());
            return Res.Fail($"按类型获取实例失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all instances by group ID
    /// </summary>
    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesByGroupAsync(string groupId)
    {
        try
        {
            var instances = _manager.GetInstancesByGroup(groupId);

            var viewModels = instances
                .Select(MapToViewModel)
                .OrderByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Retrieved {Count} instances in group {GroupId}", viewModels.Count, groupId);

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances by group: {GroupId}", groupId);
            return Res.Fail($"按组获取实例失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all instances that have exceptions
    /// </summary>
    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesWithExceptionsAsync()
    {
        try
        {
            var instances = _manager.GetInstancesWithExceptions();

            var viewModels = instances
                .Select(MapToViewModel)
                .OrderByDescending(vm => vm.ExceptionCount)
                .ThenByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Retrieved {Count} instances with exceptions", viewModels.Count);

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances with exceptions");
            return Res.Fail($"获取异常实例失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Filters instances based on filter criteria
    /// </summary>
    public async Task<Res<List<ObservableInstanceViewModel>>> FilterInstancesAsync(ObservableInstanceFilter filter)
    {
        try
        {
            var allInstances = _manager.GetAllInstances().ToList();

            // Convert to view models first to access computed properties
            var viewModels = allInstances.Select(MapToViewModel).AsQueryable();

            // Apply text search
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var searchText = filter.SearchText.ToLowerInvariant();
                viewModels = viewModels.Where(vm =>
                    vm.InstanceId.ToLowerInvariant().Contains(searchText) ||
                    vm.InstanceName.ToLowerInvariant().Contains(searchText));
            }

            // Apply type filter
            if (filter.InstanceType != null)
            {
                viewModels = viewModels.Where(vm => vm.InstanceType == filter.InstanceType);
            }

            // Apply group filter
            if (!string.IsNullOrWhiteSpace(filter.GroupId))
            {
                viewModels = viewModels.Where(vm => vm.GroupId == filter.GroupId);
            }

            // Apply health state filter
            if (filter.HealthStateFilter.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.HealthState == filter.HealthStateFilter.Value);
            }

            // Apply log level filter
            if (filter.LogLevels?.Any() == true)
            {
                viewModels = viewModels.Where(vm =>
                    vm.CurrentLogLevel.HasValue &&
                    filter.LogLevels.Contains(vm.CurrentLogLevel.Value));
            }

            // Apply quick filter: Critical/Error only
            if (filter.ShowCriticalErrorOnly)
            {
                viewModels = viewModels.Where(vm =>
                    vm.CurrentLogLevel == LogLevel.Critical ||
                    vm.CurrentLogLevel == LogLevel.Error);
            }

            // Apply quick filter: Unhealthy only
            if (filter.ShowUnhealthyOnly)
            {
                viewModels = viewModels.Where(vm => vm.IsUnhealthy);
            }

            // Apply date range filters
            if (filter.RegisteredFrom.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.RegisteredAt >= filter.RegisteredFrom.Value);
            }

            if (filter.RegisteredTo.HasValue)
            {
                viewModels = viewModels.Where(vm => vm.RegisteredAt <= filter.RegisteredTo.Value);
            }

            var results = viewModels
                .OrderByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Filtered instances: {Count} results", results.Count);

            return Res.Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to filter instances");
            return Res.Fail($"过滤实例失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets statistics for all observable instances
    /// </summary>
    public async Task<Res<ObservableInstanceStatistics>> GetStatisticsAsync()
    {
        try
        {
            var allInstances = _manager.GetAllInstances().ToList();
            var viewModels = allInstances.Select(MapToViewModel).ToList();

            // Calculate health state distribution
            var healthyCount = viewModels.Count(vm => vm.HealthState == HealthState.Healthy);
            var unhealthyCount = viewModels.Count(vm => vm.HealthState == HealthState.Unhealthy);
            var unknownCount = viewModels.Count(vm => vm.HealthState == HealthState.Unknown);

            // Calculate log level distribution
            var logLevelDistribution = viewModels
                .Where(vm => vm.CurrentLogLevel.HasValue)
                .GroupBy(vm => vm.CurrentLogLevel!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var statistics = new ObservableInstanceStatistics
            {
                TotalInstances = allInstances.Count,

                // Health state counts
                HealthyCount = healthyCount,
                UnhealthyCount = unhealthyCount,
                UnknownHealthCount = unknownCount,

                // Log level distribution
                LogLevelDistribution = logLevelDistribution,

                // Statistics
                TotalStateChanges = allInstances.Sum(i => i.TotalStateChanges),
                TotalExceptions = allInstances.Sum(i => i.TotalExceptions),
                AverageHistoryPerInstance = allInstances.Count > 0
                    ? allInstances.Average(i => i.Count)
                    : 0,
                TopTypesByCount = allInstances
                    .Where(i => i.InstanceType != null)
                    .GroupBy(i => i.InstanceType!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new TypeCount
                    {
                        TypeName = g.Key.GetCleanFullName(),
                        TypeShortName = g.Key.GetCleanName(),
                        Count = g.Count()
                    })
                    .ToList(),
                TopGroupsByCount = allInstances
                    .Where(i => !string.IsNullOrEmpty(i.GroupId))
                    .GroupBy(i => i.GroupId!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new GroupCount
                    {
                        GroupId = g.Key,
                        Count = g.Count()
                    })
                    .ToList()
            };

            logger.LogDebug(
                "Generated statistics: Total={Total}, Healthy={Healthy}, Unhealthy={Unhealthy}, Unknown={Unknown}",
                statistics.TotalInstances,
                statistics.HealthyCount,
                statistics.UnhealthyCount,
                statistics.UnknownHealthCount);

            return Res.Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate statistics");
            return Res.Fail($"统计失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets state history for an instance
    /// </summary>
    public async Task<Res<List<ObservableStateHistoryViewModel>>> GetInstanceHistoryAsync(string instanceId, int? limit = null)
    {
        try
        {
            var instance = _manager.GetInstance(instanceId);

            if (instance == null)
            {
                return Res.Fail("实例未找到");
            }

            var history = limit.HasValue && limit.Value > 0
                ? instance.GetRecentHistory(limit.Value)
                : instance.GetHistory();

            var viewModels = history
                .Select(MapToHistoryViewModel)
                .OrderByDescending(vm => vm.Timestamp)
                .ThenByDescending(vm => vm.Sequence)
                .ToList();

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instance history: {InstanceId}", instanceId);
            return Res.Fail($"获取历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets exceptions for an instance
    /// </summary>
    public async Task<Res<List<ObservableStateHistoryViewModel>>> GetInstanceExceptionsAsync(string instanceId)
    {
        try
        {
            var instance = _manager.GetInstance(instanceId);

            if (instance == null)
            {
                return Res.Fail("实例未找到");
            }

            var exceptions = instance.GetExceptions();

            var viewModels = exceptions
                .Select(MapToHistoryViewModel)
                .OrderByDescending(vm => vm.Timestamp)
                .ThenByDescending(vm => vm.Sequence)
                .ToList();

            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instance exceptions: {InstanceId}", instanceId);
            return Res.Fail($"获取异常失败: {ex.Message}");
        }
    }

    #endregion

    #region Management Methods

    /// <summary>
    /// Clears state history for an instance
    /// </summary>
    public async Task<Res> ClearInstanceHistoryAsync(string instanceId)
    {
        try
        {
            var instance = _manager.GetInstance(instanceId);

            if (instance == null)
            {
                return Res.Fail("实例未找到");
            }

            instance.Clear();

            logger.LogInformation("Cleared history for instance: {InstanceId}", instanceId);

            return Res.Ok("历史记录已清除");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear instance history: {InstanceId}", instanceId);
            return Res.Fail($"清除历史失败: {ex.Message}");
        }
    }

    #endregion

    #region Mapping Methods

    /// <summary>
    /// Maps ObservableAgent to ViewModel
    /// </summary>
    private ObservableInstanceViewModel MapToViewModel(ObservableAgent instance)
    {
        return new ObservableInstanceViewModel
        {
            InstanceId = instance.InstanceId,
            InstanceName = instance.InstanceName,
            InstanceType = instance.InstanceType,
            InstanceKey = instance.InstanceKey,
            GroupId = instance.GroupId,
            CurrentState = instance.CurrentState,
            StateChangedAt = instance.StateChangedAt,
            RegisteredAt = instance.RegisteredAt,
            TotalStateChanges = instance.TotalStateChanges,
            TotalExceptions = instance.TotalExceptions,
            HistoryCount = instance.Count,
            ExceptionCount = instance.ExceptionCount,
            HasExceptions = instance.HasExceptions,
            CurrentLogLevel = instance.CurrentLogLevel,
            IsUnhealthy = instance.IsUnhealthy()
        };
    }

    /// <summary>
    /// Maps ObservableStateHistory to ViewModel
    /// </summary>
    private ObservableStateHistoryViewModel MapToHistoryViewModel(ObservableStateHistory history)
    {
        return new ObservableStateHistoryViewModel
        {
            Sequence = history.Sequence,
            Timestamp = history.Timestamp,
            PreviousState = history.PreviousState,
            CurrentState = history.CurrentState,
            Message = history.Message,
            Exception = history.Exception,
            LogLevel = history.LogLevel
        };
    }

    #endregion
}
