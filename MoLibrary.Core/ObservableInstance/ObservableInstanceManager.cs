using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Modules;

namespace MoLibrary.Core.ObservableInstance;

/// <summary>
/// Default implementation of IObservableInstanceManager.
/// Provides centralized management and query capabilities for all observable instances.
/// </summary>
public class ObservableInstanceManager(IOptions<ModuleObservableInstanceOption> globalOptions) : IObservableInstanceManager
{
    private readonly ConcurrentDictionary<string, ObservableAgent> _instances = new();

    /// <summary>
    /// Creates a new ObservableAgent instance
    /// </summary>
    public ObservableAgent Create(string instanceId, Action<ObservableAgentOption>? configure = null)
    {
        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentException("Instance ID cannot be empty", nameof(instanceId));

        var instanceOption = new ObservableAgentOption { InstanceId = instanceId };
        configure?.Invoke(instanceOption);

        var globalOption = globalOptions.Value;
        var maxHistorySize = instanceOption.MaxHistorySize ?? globalOption.DefaultMaxHistorySize;

        var agent = new ObservableAgent(instanceId, maxHistorySize)
        {
            InstanceName = instanceOption.InstanceName ?? instanceId,
            InstanceType = instanceOption.InstanceType,
            InstanceKey = instanceOption.InstanceKey,
            GroupId = instanceOption.GroupId
        };
        _instances.TryAdd(agent.InstanceId, agent);
        return agent;
    }

   
    /// <summary>
    /// Gets all registered observable instances
    /// </summary>
    public IReadOnlyList<ObservableAgent> GetAllInstances()
    {
        return _instances.Values.ToList();
    }

    /// <summary>
    /// Gets an observable instance by instance ID
    /// </summary>
    public ObservableAgent? GetInstance(string instanceId)
    {
        return _instances.TryGetValue(instanceId, out var agent) ? agent : null;
    }

    /// <summary>
    /// Gets all instances by type
    /// </summary>
    public IReadOnlyList<ObservableAgent> GetInstancesByType(Type type)
    {
        return _instances.Values
            .Where(a => a.InstanceType == type)
            .ToList();
    }

    /// <summary>
    /// Gets all instances by group ID
    /// </summary>
    public IReadOnlyList<ObservableAgent> GetInstancesByGroup(string groupId)
    {
        return _instances.Values
            .Where(a => a.GroupId == groupId)
            .ToList();
    }

    /// <summary>
    /// Gets all instances that have exceptions
    /// </summary>
    public IReadOnlyList<ObservableAgent> GetInstancesWithExceptions()
    {
        return _instances.Values
            .Where(a => a.HasExceptions)
            .ToList();
    }

    /// <summary>
    /// Gets all instances in a specific state (for typed states)
    /// </summary>
    public IReadOnlyList<ObservableAgent> GetInstancesByState<TState>(TState state) where TState : notnull
    {
        return _instances.Values
            .Where(a => a.CurrentState != null && a.CurrentState.Equals(state))
            .ToList();
    }
}
