using Microsoft.Extensions.Logging;

using Monica.Core.ObservableInstance.Facades;
using Monica.Core.ObservableInstance.Models;
using Monica.Framework.UI.UIObservableInstance.Models;
using Monica.Tool.Extensions;
using Monica.Core.Results;

namespace Monica.Framework.UI.UIObservableInstance.Services;

/// <summary>
/// UI support service that maps observable instance facade results to UI view models.
/// </summary>
public sealed class ObservableInstanceMonitorService(
    ObservableInstanceFacade observableInstanceFacade,
    ILogger<ObservableInstanceMonitorService> logger)
{
    private readonly ObservableInstanceFacade _facade = observableInstanceFacade;

    public async Task<Res<List<ObservableInstanceViewModel>>> GetAllInstancesAsync()
    {
        var result = await _facade.GetAllInstancesAsync();
        if (result.IsFailed(out var error, out var instances))
        {
            return Res.Fail(error);
        }

        var viewModels = (instances ?? [])
            .Select(MapToViewModel)
            .OrderByDescending(vm => vm.RegisteredAt)
            .ToList();

        logger.LogDebug("Retrieved {Count} observable instances", viewModels.Count);
        return Res.Ok(viewModels);
    }

    public async Task<Res<ObservableInstanceViewModel?>> GetInstanceByIdAsync(string instanceId)
    {
        var result = await _facade.GetByIdAsync(instanceId);
        if (result.IsFailed(out var error, out var instance))
        {
            return Res.Fail(error);
        }

        return Res.Ok(instance == null ? null : MapToViewModel(instance));
    }

    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesByTypeAsync(Type type)
    {
        var result = await _facade.GetInstancesByTypeAsync(type);
        if (result.IsFailed(out var error, out var instances))
        {
            return Res.Fail(error);
        }

        var viewModels = (instances ?? [])
            .Select(MapToViewModel)
            .OrderByDescending(vm => vm.RegisteredAt)
            .ToList();

        logger.LogDebug("Retrieved {Count} instances of type {Type}", viewModels.Count, type.GetCleanName());
        return Res.Ok(viewModels);
    }

    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesByGroupAsync(string groupId)
    {
        var result = await _facade.GetInstancesByGroupAsync(groupId);
        if (result.IsFailed(out var error, out var instances))
        {
            return Res.Fail(error);
        }

        var viewModels = (instances ?? [])
            .Select(MapToViewModel)
            .OrderByDescending(vm => vm.RegisteredAt)
            .ToList();

        logger.LogDebug("Retrieved {Count} instances in group {GroupId}", viewModels.Count, groupId);
        return Res.Ok(viewModels);
    }

    public async Task<Res<List<ObservableInstanceViewModel>>> GetInstancesWithExceptionsAsync()
    {
        var result = await _facade.GetInstancesWithExceptionsAsync();
        if (result.IsFailed(out var error, out var instances))
        {
            return Res.Fail(error);
        }

        var viewModels = (instances ?? [])
            .Select(MapToViewModel)
            .OrderByDescending(vm => vm.ExceptionCount)
            .ThenByDescending(vm => vm.RegisteredAt)
            .ToList();

        logger.LogDebug("Retrieved {Count} instances with exceptions", viewModels.Count);
        return Res.Ok(viewModels);
    }

    public async Task<Res<List<ObservableInstanceViewModel>>> FilterInstancesAsync(ObservableInstanceFilter filter)
    {
        var result = await GetAllInstancesAsync();
        if (result.IsFailed(out var error, out var viewModels))
        {
            return Res.Fail(error);
        }

        var query = (viewModels ?? []).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var searchText = filter.SearchText.ToLowerInvariant();
            query = query.Where(vm =>
                vm.InstanceId.ToLowerInvariant().Contains(searchText) ||
                vm.InstanceName.ToLowerInvariant().Contains(searchText));
        }

        if (filter.InstanceType != null)
        {
            query = query.Where(vm => vm.InstanceType == filter.InstanceType);
        }

        if (!string.IsNullOrWhiteSpace(filter.GroupId))
        {
            query = query.Where(vm => vm.GroupId == filter.GroupId);
        }

        if (filter.HealthStateFilter.HasValue)
        {
            query = query.Where(vm => vm.HealthState == filter.HealthStateFilter.Value);
        }

        if (filter.LogLevels?.Any() == true)
        {
            query = query.Where(vm =>
                vm.CurrentLogLevel.HasValue &&
                filter.LogLevels.Contains(vm.CurrentLogLevel.Value));
        }

        if (filter.ShowCriticalErrorOnly)
        {
            query = query.Where(vm =>
                vm.CurrentLogLevel == LogLevel.Critical ||
                vm.CurrentLogLevel == LogLevel.Error);
        }

        if (filter.ShowUnhealthyOnly)
        {
            query = query.Where(vm => vm.IsUnhealthy);
        }

        if (filter.RegisteredFrom.HasValue)
        {
            query = query.Where(vm => vm.RegisteredAt >= filter.RegisteredFrom.Value);
        }

        if (filter.RegisteredTo.HasValue)
        {
            query = query.Where(vm => vm.RegisteredAt <= filter.RegisteredTo.Value);
        }

        var filtered = query
            .OrderByDescending(vm => vm.RegisteredAt)
            .ToList();

        logger.LogDebug("Filtered observable instances: {Count} results", filtered.Count);
        return Res.Ok(filtered);
    }

    public async Task<Res<ObservableInstanceStatistics>> GetStatisticsAsync()
    {
        var result = await _facade.GetAllInstancesAsync();
        if (result.IsFailed(out var error, out var instances))
        {
            return Res.Fail(error);
        }

        var allInstances = (instances ?? []).ToList();
        var viewModels = allInstances.Select(MapToViewModel).ToList();

        var healthyCount = viewModels.Count(vm => vm.HealthState == HealthState.Healthy);
        var unhealthyCount = viewModels.Count(vm => vm.HealthState == HealthState.Unhealthy);
        var unknownCount = viewModels.Count(vm => vm.HealthState == HealthState.Unknown);

        var logLevelDistribution = viewModels
            .Where(vm => vm.CurrentLogLevel.HasValue)
            .GroupBy(vm => vm.CurrentLogLevel!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var statistics = new ObservableInstanceStatistics
        {
            TotalInstances = allInstances.Count,
            HealthyCount = healthyCount,
            UnhealthyCount = unhealthyCount,
            UnknownHealthCount = unknownCount,
            LogLevelDistribution = logLevelDistribution,
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
            "Generated observable instance statistics: Total={Total}, Healthy={Healthy}, Unhealthy={Unhealthy}, Unknown={Unknown}",
            statistics.TotalInstances,
            statistics.HealthyCount,
            statistics.UnhealthyCount,
            statistics.UnknownHealthCount);

        return Res.Ok(statistics);
    }

    public async Task<Res<List<ObservableStateHistoryViewModel>>> GetInstanceHistoryAsync(string instanceId, int? limit = null)
    {
        var result = await _facade.GetInstanceHistoryAsync(instanceId, limit);
        if (result.IsFailed(out var error, out var history))
        {
            return Res.Fail(error);
        }

        var viewModels = (history ?? [])
            .Select(MapToHistoryViewModel)
            .OrderByDescending(vm => vm.Timestamp)
            .ThenByDescending(vm => vm.Sequence)
            .ToList();

        return Res.Ok(viewModels);
    }

    public async Task<Res<List<ObservableStateHistoryViewModel>>> GetInstanceExceptionsAsync(string instanceId)
    {
        var result = await _facade.GetInstanceExceptionsAsync(instanceId);
        if (result.IsFailed(out var error, out var exceptions))
        {
            return Res.Fail(error);
        }

        var viewModels = (exceptions ?? [])
            .Select(MapToHistoryViewModel)
            .OrderByDescending(vm => vm.Timestamp)
            .ThenByDescending(vm => vm.Sequence)
            .ToList();

        return Res.Ok(viewModels);
    }

    public Task<Res> ClearInstanceHistoryAsync(string instanceId)
    {
        return _facade.ClearInstanceHistoryAsync(instanceId);
    }

    private ObservableInstanceViewModel MapToViewModel(ObservableInstanceTracker instance)
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

    private ObservableStateHistoryViewModel MapToHistoryViewModel(ObservableStateEntry history)
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
}
