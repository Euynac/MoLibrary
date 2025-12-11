using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.FrameworkUI.UIObservableInstance.Models;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UIObservableInstance.Services;

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
            var allInstances = _manager.GetAllInstances().AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var searchText = filter.SearchText.ToLowerInvariant();
                allInstances = allInstances.Where(i =>
                    i.InstanceId.ToLowerInvariant().Contains(searchText) ||
                    i.InstanceName.ToLowerInvariant().Contains(searchText));
            }

            if (filter.InstanceType != null)
            {
                allInstances = allInstances.Where(i => i.InstanceType == filter.InstanceType);
            }

            if (!string.IsNullOrWhiteSpace(filter.GroupId))
            {
                allInstances = allInstances.Where(i => i.GroupId == filter.GroupId);
            }

            if (filter.HasExceptions.HasValue)
            {
                allInstances = allInstances.Where(i => i.HasExceptions == filter.HasExceptions.Value);
            }

            if (filter.RegisteredFrom.HasValue)
            {
                allInstances = allInstances.Where(i => i.RegisteredAt >= filter.RegisteredFrom.Value);
            }

            if (filter.RegisteredTo.HasValue)
            {
                allInstances = allInstances.Where(i => i.RegisteredAt <= filter.RegisteredTo.Value);
            }

            var viewModels = allInstances
                .ToList()
                .Select(MapToViewModel)
                .OrderByDescending(vm => vm.RegisteredAt)
                .ToList();

            logger.LogDebug("Filtered instances: {Count} results", viewModels.Count);

            return Res.Ok(viewModels);
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

            var statistics = new ObservableInstanceStatistics
            {
                TotalInstances = allInstances.Count,
                InstancesWithExceptions = allInstances.Count(i => i.HasExceptions),
                InstancesWithoutExceptions = allInstances.Count(i => !i.HasExceptions),
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

            logger.LogDebug("Generated statistics: Total={Total}, WithExceptions={WithExceptions}",
                statistics.TotalInstances, statistics.InstancesWithExceptions);

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
            HasExceptions = instance.HasExceptions
        };
    }

    /// <summary>
    /// Maps ObservableStateHistory to ViewModel
    /// </summary>
    private ObservableStateHistoryViewModel MapToHistoryViewModel(ObservableStateHistory history)
    {
        return new ObservableStateHistoryViewModel
        {
            Timestamp = history.Timestamp,
            PreviousState = history.PreviousState,
            CurrentState = history.CurrentState,
            Message = history.Message,
            Exception = history.Exception
        };
    }

    #endregion
}
