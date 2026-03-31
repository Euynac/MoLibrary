namespace Monica.StateStore.TaskProgress.Models;

/// <summary>
/// Captures the persisted status snapshot for a progress-tracked task.
/// </summary>
public class TaskProgressStatus(int totalSteps, string id)
{
    /// <summary>
    /// Gets the total number of logical steps in the task.
    /// </summary>
    public int TotalSteps { get; } = totalSteps;

    /// <summary>
    /// Gets the unique task identifier.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets or sets the current completed step count.
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets the elapsed execution time since the task started.
    /// </summary>
    public TimeSpan ElapsedTime => CurrentStep >= TotalSteps ? LastUpdated - StartTime : DateTime.Now - StartTime;

    /// <summary>
    /// Gets or sets the detailed progress message.
    /// </summary>
    public string? CurrentStatus { get; set; }

    /// <summary>
    /// Gets or sets the coarse-grained task phase.
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// Gets a value indicating whether the task has reached a terminal state.
    /// </summary>
    public virtual bool IsEnd => IsCancelled || CurrentStep >= TotalSteps;

    /// <summary>
    /// Gets or sets a value indicating whether the task was cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Gets or sets the cancellation reason.
    /// </summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// Gets or sets the last status update timestamp.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the task start timestamp.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets the completion percentage rounded to two decimal places.
    /// </summary>
    public virtual double Percentage => TotalSteps > 0
        ? Math.Round((double)CurrentStep / TotalSteps * 100, 2)
        : 0;

    /// <summary>
    /// Gets the estimated remaining time based on the current execution rate.
    /// </summary>
    public TimeSpan? EstimatedRemaining => CalculateRemainingTime();

    /// <summary>
    /// Calculates the estimated remaining time based on the current execution rate.
    /// </summary>
    /// <returns>
    /// The estimated remaining time, or <see langword="null" /> when progress has not advanced far enough to estimate.
    /// </returns>
    public virtual TimeSpan? CalculateRemainingTime()
    {
        if (CurrentStep <= 0) return null;

        var elapsedPerStep = ElapsedTime.TotalMilliseconds / CurrentStep;
        var remainingSteps = TotalSteps - CurrentStep;
        return TimeSpan.FromMilliseconds(elapsedPerStep * remainingSteps);
    }
}
