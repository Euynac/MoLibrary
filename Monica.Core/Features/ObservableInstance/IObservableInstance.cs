namespace Monica.Core.Features.ObservableInstance;

/// <summary>
/// Interface for instances that can be observed with state and exception tracking
/// </summary>
public interface IObservableInstance
{
    /// <summary>
    /// Gets or sets the observable agent for this instance
    /// </summary>
    ObservableAgent ObservableAgent { get; set; }
}
