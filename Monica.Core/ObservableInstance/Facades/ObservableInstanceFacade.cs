using Microsoft.Extensions.Logging;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Models;
using Monica.Tool.Results;

namespace Monica.Core.ObservableInstance.Facades;

/// <summary>
/// Facade wrapper for observable instance queries and maintenance operations.
/// </summary>
public sealed class ObservableInstanceFacade(
    IObservableInstanceRegistry observableInstanceRegistry,
    ILogger<ObservableInstanceFacade> logger)
{
    private const string InstanceNotFoundMessage = "Observable instance was not found.";
    private readonly IObservableInstanceRegistry _registry = observableInstanceRegistry;

    public Task<Res<IReadOnlyList<ObservableInstanceTracker>>> GetAllInstancesAsync()
    {
        try
        {
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Ok(_registry.GetAllInstances()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all observable instances.");
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Fail($"Failed to get observable instances: {ex.Message}"));
        }
    }

    public Task<Res<ObservableInstanceTracker?>> GetByIdAsync(string instanceId)
    {
        try
        {
            return Task.FromResult<Res<ObservableInstanceTracker?>>(Res.Ok(_registry.GetById(instanceId)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instance {InstanceId}.", instanceId);
            return Task.FromResult<Res<ObservableInstanceTracker?>>(Res.Fail($"Failed to get observable instance details: {ex.Message}"));
        }
    }

    public Task<Res<IReadOnlyList<ObservableInstanceTracker>>> GetInstancesByTypeAsync(Type type)
    {
        try
        {
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Ok(_registry.GetInstancesByType(type)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instances by type {Type}.", type);
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Fail($"Failed to get observable instances by type: {ex.Message}"));
        }
    }

    public Task<Res<IReadOnlyList<ObservableInstanceTracker>>> GetInstancesByGroupAsync(string groupId)
    {
        try
        {
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Ok(_registry.GetInstancesByGroup(groupId)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instances by group {GroupId}.", groupId);
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Fail($"Failed to get observable instances by group: {ex.Message}"));
        }
    }

    public Task<Res<IReadOnlyList<ObservableInstanceTracker>>> GetInstancesWithExceptionsAsync()
    {
        try
        {
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Ok(_registry.GetInstancesWithExceptions()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instances with exceptions.");
            return Task.FromResult<Res<IReadOnlyList<ObservableInstanceTracker>>>(Res.Fail($"Failed to get observable instances with exceptions: {ex.Message}"));
        }
    }

    public Task<Res<IReadOnlyList<ObservableStateEntry>>> GetInstanceHistoryAsync(string instanceId, int? limit = null)
    {
        try
        {
            var tracker = _registry.GetById(instanceId);
            if (tracker == null)
            {
                return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Fail(InstanceNotFoundMessage));
            }

            IReadOnlyList<ObservableStateEntry> history = limit.HasValue && limit.Value > 0
                ? tracker.GetRecentHistory(limit.Value)
                : tracker.GetHistory();

            return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Ok(history));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instance history for {InstanceId}.", instanceId);
            return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Fail($"Failed to get observable instance history: {ex.Message}"));
        }
    }

    public Task<Res<IReadOnlyList<ObservableStateEntry>>> GetInstanceExceptionsAsync(string instanceId)
    {
        try
        {
            var tracker = _registry.GetById(instanceId);
            if (tracker == null)
            {
                return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Fail(InstanceNotFoundMessage));
            }

            return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Ok(tracker.GetExceptions()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get observable instance exceptions for {InstanceId}.", instanceId);
            return Task.FromResult<Res<IReadOnlyList<ObservableStateEntry>>>(Res.Fail($"Failed to get observable instance exceptions: {ex.Message}"));
        }
    }

    public Task<Res> ClearInstanceHistoryAsync(string instanceId)
    {
        try
        {
            var tracker = _registry.GetById(instanceId);
            if (tracker == null)
            {
                return Task.FromResult<Res>(Res.Fail(InstanceNotFoundMessage));
            }

            tracker.ClearHistory();
            return Task.FromResult<Res>(Res.Ok("Observable instance history was cleared."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear observable instance history for {InstanceId}.", instanceId);
            return Task.FromResult<Res>(Res.Fail($"Failed to clear observable instance history: {ex.Message}"));
        }
    }
}
