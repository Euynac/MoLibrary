namespace Monica.StateStore.TaskProgress.Models;

/// <summary>
/// Defines persistence and update behavior for a progress-tracked task.
/// </summary>
public class TaskProgressSetting
{
    /// <summary>
    /// Controls how often status snapshots are flushed automatically.
    /// Leave this value <see langword="null" /> to persist every update immediately.
    /// </summary>
    public TimeSpan? AutoUpdateDuration { get; set; }

    /// <summary>
    /// Indicates whether the task state should be stored in the shared state store
    /// so it can be observed or resumed across process boundaries.
    /// </summary>
    public bool UseDistributedTaskProgress { get; set; }

    /// <summary>
    /// Sets the total number of logical steps used to compute completion percentage.
    /// Defaults to 100.
    /// </summary>
    public int TotalSteps { get; set; } = 100;

    /// <summary>
    /// Sets how long an active progress snapshot remains in storage without updates.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Sets how long a completed progress snapshot remains in storage after completion.
    /// Defaults to 3 minutes.
    /// </summary>
    public TimeSpan CompletedTimeToLive { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Stores the distributed ownership stamp for the active writer.
    /// This value is managed by the infrastructure layer.
    /// </summary>
    internal string? DistributedStamp { get; set; }
}
