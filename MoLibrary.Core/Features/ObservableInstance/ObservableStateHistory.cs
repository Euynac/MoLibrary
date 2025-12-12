using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.ObservableInstance;

/// <summary>
/// Represents a single state change entry in the observable history.
/// Unifies state tracking and exception tracking.
/// </summary>
public class ObservableStateHistory
{
    /// <summary>
    /// Gets the timestamp when this state change occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the previous state before this change (null for initial state or if not applicable)
    /// </summary>
    public object? PreviousState { get; init; }

    /// <summary>
    /// Gets the current state after this change
    /// For exception-only entries, this may be null or same as previous
    /// </summary>
    public object? CurrentState { get; init; }

    /// <summary>
    /// Gets a descriptive message about this state change
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception associated with this state change (if any)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets the log level associated with this state at the time of recording (if mapped)
    /// </summary>
    public LogLevel? LogLevel { get; init; }

    /// <summary>
    /// Gets whether this is an exception entry
    /// </summary>
    public bool IsException => Exception != null;

    /// <summary>
    /// Gets whether this is a state transition (state changed)
    /// </summary>
    public bool IsStateTransition => !Equals(PreviousState, CurrentState);
}
