namespace Monica.JobScheduler.Models;

/// <summary>
/// job health indicators
/// </summary>
public class JobHealthMetrics
{
    public string JobKey { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }

    // State count properties
    public int SucceededCount { get; set; }
    public int SkippedCount { get; set; }
    public int CancelledCount { get; set; }
    public int ProcessingCount { get; set; }
    public int FailedCount { get; set; }
    public int TerminatedCount { get; set; }
    public int EnqueuedCount { get; set; }
    public int ScheduledCount { get; set; }

    /// <summary>
    /// Healthiness - measures the effectiveness of job scheduling
    /// Formula: (total number of executions - skipped - failed - terminated) / total number of executions * 100
    /// </summary>
    public double HealthScore => TotalExecutions > 0
        ? ((TotalExecutions - SkippedCount - FailedCount - TerminatedCount) / (double)TotalExecutions) * 100
        : 100;

    /// <summary>
    /// Execution Failure Rate - Measures the reliability of a job's code
    /// Formula: (failure + termination) / actual number of completions * 100
    /// Actual number of completions = total number of executions - skipped - canceled - running - queued - scheduled
    /// </summary>
    public double ExecutionFailureRate
    {
        get
        {
            var completedExecutions = TotalExecutions - SkippedCount - CancelledCount - ProcessingCount - EnqueuedCount - ScheduledCount;
            if (completedExecutions <= 0) return 0;
            return ((FailedCount + TerminatedCount) / (double)completedExecutions) * 100;
        }
    }

    public List<JobInstance> RecentFailures { get; set; } = new();
    public DateTime MetricsStartTime { get; set; }
    public DateTime MetricsEndTime { get; set; }
}
