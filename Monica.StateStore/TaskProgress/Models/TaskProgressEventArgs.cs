namespace Monica.StateStore.TaskProgress.Models;

/// <summary>
/// Provides event data for progress updates.
/// </summary>
public class TaskProgressEventArgs(TaskProgress taskProgress) : EventArgs
{
    /// <summary>
    /// Gets the task progress instance that raised the event.
    /// </summary>
    public TaskProgress TaskProgress { get; } = taskProgress;
}

/// <summary>
/// Provides event data for progress cancellation.
/// </summary>
public class TaskProgressCancelledEventArgs(TaskProgress taskProgress, string? reason = null) : EventArgs
{
    /// <summary>
    /// Gets the task progress instance that raised the event.
    /// </summary>
    public TaskProgress TaskProgress { get; } = taskProgress;

    /// <summary>
    /// Gets the optional cancellation reason supplied by the caller.
    /// </summary>
    public string? Reason { get; } = reason;
}
