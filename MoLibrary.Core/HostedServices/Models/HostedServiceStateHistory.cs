namespace MoLibrary.Core.HostedServices.Models;

/// <summary>
/// Represents a single state transition entry in the service history
/// </summary>
public class HostedServiceStateHistory
{
    /// <summary>
    /// Gets the timestamp when this state change occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the previous state before this transition (null for initial state)
    /// </summary>
    public HostedServiceState? PreviousState { get; init; }

    /// <summary>
    /// Gets the new state after this transition
    /// </summary>
    public HostedServiceState CurrentState { get; init; }

    /// <summary>
    /// Gets a descriptive message about this state change
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception associated with this state change (if any)
    /// </summary>
    public Exception? Exception { get; init; }
}
