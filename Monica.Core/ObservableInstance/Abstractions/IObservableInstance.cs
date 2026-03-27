using Monica.Core.ObservableInstance.Models;

namespace Monica.Core.ObservableInstance.Abstractions;

/// <summary>
/// Interface for instances that can be observed with state and exception tracking
/// </summary>
public interface IObservableInstance
{
    /// <summary>
    /// Gets or sets the observable tracker for this instance
    /// </summary>
    ObservableInstanceTracker ObservableTracker { get; set; }
}
