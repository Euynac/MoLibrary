using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Core.ObservableInstance.Models;
using Monica.Modules;

namespace Monica.Core.ObservableInstance.Services;

/// <summary>
/// Default registry for observable instance trackers.
/// </summary>
public class ObservableInstanceRegistry(IOptions<ModuleObservableInstanceOption> globalOptions) : IObservableInstanceRegistry
{
    private readonly ConcurrentDictionary<string, ObservableInstanceTracker> _instances = new();

    /// <summary>
    /// Registers a new observable instance tracker.
    /// </summary>
    public ObservableInstanceTracker Register(string instanceId, Action<ObservableInstanceRegistration>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instanceOption = new ObservableInstanceRegistration { InstanceId = instanceId };
        configure?.Invoke(instanceOption);

        var globalOption = globalOptions.Value;
        var maxHistorySize = instanceOption.MaxHistorySize ?? globalOption.DefaultMaxHistorySize;

        var tracker = new ObservableInstanceTracker(instanceId, maxHistorySize, instanceOption)
        {
            InstanceName = instanceOption.InstanceName ?? instanceId,
            InstanceType = instanceOption.InstanceType,
            InstanceKey = instanceOption.InstanceKey,
            GroupId = instanceOption.GroupId
        };

        if (!_instances.TryAdd(tracker.InstanceId, tracker))
        {
            tracker.Dispose();
            throw new InvalidOperationException($"Observable instance '{tracker.InstanceId}' is already registered.");
        }

        return tracker;
    }

   
    /// <summary>
    /// Gets all registered observable instances
    /// </summary>
    public IReadOnlyList<ObservableInstanceTracker> GetAllInstances()
    {
        return _instances.Values.ToList();
    }

    /// <summary>
    /// Gets an observable instance by instance ID
    /// </summary>
    public ObservableInstanceTracker? GetById(string instanceId)
    {
        return _instances.GetValueOrDefault(instanceId);
    }

    /// <summary>
    /// Gets all instances by type
    /// </summary>
    public IReadOnlyList<ObservableInstanceTracker> GetInstancesByType(Type type)
    {
        return _instances.Values
            .Where(a => a.InstanceType == type)
            .ToList();
    }

    /// <summary>
    /// Gets all instances by group ID
    /// </summary>
    public IReadOnlyList<ObservableInstanceTracker> GetInstancesByGroup(string groupId)
    {
        return _instances.Values
            .Where(a => a.GroupId == groupId)
            .ToList();
    }

    /// <summary>
    /// Gets all instances that have exceptions
    /// </summary>
    public IReadOnlyList<ObservableInstanceTracker> GetInstancesWithExceptions()
    {
        return _instances.Values
            .Where(a => a.HasExceptions)
            .ToList();
    }

    /// <summary>
    /// Gets all instances in a specific state (for typed states)
    /// </summary>
    public IReadOnlyList<ObservableInstanceTracker> GetInstancesByState<TState>(TState state) where TState : notnull
    {
        return _instances.Values
            .Where(a => a.CurrentState != null && a.CurrentState.Equals(state))
            .ToList();
    }
}
