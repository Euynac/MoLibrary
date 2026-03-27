namespace Monica.StateStore.ProgressBar;

public class ProgressBarStatus(int totalSteps, string id)
{
    /// <summary>
    /// Total steps
    /// </summary>
    public int TotalSteps { get; } = totalSteps;

    /// <summary>
    /// Progress bar job ID
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Current progress steps
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// time that has passed
    /// </summary>
    public TimeSpan ElapsedTime => CurrentStep >= TotalSteps ? LastUpdated - StartTime : DateTime.Now - StartTime;

    /// <summary>
    /// Current status description (detailed progress description)
    /// </summary>
    public string? CurrentStatus { get; set; }

    /// <summary>
    /// Current stage (coarse-grained progress stage)
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// has ended
    /// </summary>
    public virtual bool IsEnd => IsCancelled || CurrentStep >= TotalSteps;

    /// <summary>
    /// Has it been cancelled?
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Reason for cancellation
    /// </summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// Last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// Task start time
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Progress percentage, calculate the current number of steps as a percentage of the total number of steps, keep two decimal places
    /// </summary>
    public virtual double Percentage => TotalSteps > 0 ?
        Math.Round((double) CurrentStep / TotalSteps * 100, 2) : 0;

    /// <summary>
    /// Estimated remaining time, calculate remaining completion time based on current progress
    /// </summary>
    public TimeSpan? EstimatedRemaining => CalculateRemainingTime();
    /// <summary>
    /// Calculate estimated remaining completion time
    /// </summary>
    /// <returns>Estimated remaining time, if the current number of steps is less than or equal to 0, return null</returns>
    public virtual TimeSpan? CalculateRemainingTime()
    {
        if (CurrentStep <= 0) return null;

        var elapsedPerStep = ElapsedTime.TotalMilliseconds / CurrentStep;
        var remainingSteps = TotalSteps - CurrentStep;
        return TimeSpan.FromMilliseconds(elapsedPerStep * remainingSteps);
    }
}