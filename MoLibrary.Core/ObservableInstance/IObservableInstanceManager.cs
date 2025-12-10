namespace MoLibrary.Core.ObservableInstance;

/// <summary>
/// Provides centralized management and query capabilities for all observable instances.
/// Replaces IExceptionPoolManager functionality.
/// </summary>
public interface IObservableInstanceManager
{
    /// <summary>
    /// Creates a new ObservableAgent instance
    /// </summary>
    /// <param name="instanceId">Unique instance identifier</param>
    /// <param name="configure">Configuration delegate (optional)</param>
    /// <returns>Configured ObservableAgent instance</returns>
    ObservableAgent Create(string instanceId, Action<ObservableAgentOption>? configure = null);

    /// <summary>
    /// Gets all registered observable instances
    /// </summary>
    IReadOnlyList<ObservableAgent> GetAllInstances();

    /// <summary>
    /// Gets an observable instance by instance ID
    /// </summary>
    ObservableAgent? GetInstance(string instanceId);

    /// <summary>
    /// Gets all instances by type
    /// </summary>
    IReadOnlyList<ObservableAgent> GetInstancesByType(Type type);

    /// <summary>
    /// Gets all instances by group ID
    /// </summary>
    IReadOnlyList<ObservableAgent> GetInstancesByGroup(string groupId);

    /// <summary>
    /// Gets all instances that have exceptions
    /// </summary>
    IReadOnlyList<ObservableAgent> GetInstancesWithExceptions();

    /// <summary>
    /// Gets all instances in a specific state (for typed states)
    /// </summary>
    IReadOnlyList<ObservableAgent> GetInstancesByState<TState>(TState state) where TState : notnull;
}
