namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Internal model for tracking long-interval recurring jobs.
/// Used when cron interval exceeds Timer safety threshold.
/// </summary>
internal class LongIntervalRecurringSchedule
{
    /// <summary>
    /// Job unique key (JobDefinition.JobKey)
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Next scheduled execution time (UTC)
    /// </summary>
    public required DateTime NextScheduledTime { get; set; }

    /// <summary>
    /// Last calculated time (for tracking calculation updates)
    /// </summary>
    public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
}
