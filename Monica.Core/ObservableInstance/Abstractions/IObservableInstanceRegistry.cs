using Monica.Core.ObservableInstance.Models;

namespace Monica.Core.ObservableInstance.Abstractions;

/// <summary>
/// Provides centralized management and query capabilities for all observable instances.
/// Replaces IExceptionPoolManager functionality.
/// </summary>
public interface IObservableInstanceRegistry
{
    /// <summary>
    /// Registers a new observable instance tracker.
    /// </summary>
    /// <param name="instanceId">Unique instance identifier</param>
    /// <param name="configure">Configuration delegate (optional)</param>
    /// <returns>The registered tracker</returns>
    ObservableInstanceTracker Register(string instanceId, Action<ObservableInstanceRegistration>? configure = null);

    /// <summary>
    /// Gets all registered observable instances
    /// </summary>
    IReadOnlyList<ObservableInstanceTracker> GetAllInstances();

    /// <summary>
    /// Gets an observable instance by instance ID
    /// </summary>
    ObservableInstanceTracker? GetById(string instanceId);

    /// <summary>
    /// Gets all instances by type
    /// </summary>
    IReadOnlyList<ObservableInstanceTracker> GetInstancesByType(Type type);

    /// <summary>
    /// Gets all instances by group ID
    /// </summary>
    IReadOnlyList<ObservableInstanceTracker> GetInstancesByGroup(string groupId);

    /// <summary>
    /// Gets all instances that have exceptions
    /// </summary>
    IReadOnlyList<ObservableInstanceTracker> GetInstancesWithExceptions();

    /// <summary>
    /// Gets all instances in a specific state (for typed states)
    /// </summary>
    IReadOnlyList<ObservableInstanceTracker> GetInstancesByState<TState>(TState state) where TState : notnull;
}
