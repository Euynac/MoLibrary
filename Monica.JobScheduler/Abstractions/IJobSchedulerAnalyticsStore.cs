using Monica.JobScheduler.Models.Analytics;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Provides bounded, database-aggregated scheduler execution analytics.
/// </summary>
public interface IJobSchedulerAnalyticsStore
{
    /// <summary>
    /// Gets execution cohort, completion outcome, duration, trend, ranking, and slow-attempt analytics for one
    /// scheduler scope without loading its unbounded execution history.
    /// </summary>
    /// <param name="schedulerScopeKey">The scheduler scope to aggregate.</param>
    /// <param name="query">The validated time, bucket, job, and result bounds.</param>
    /// <param name="cancellationToken">Cancels the read operation.</param>
    /// <returns>One bounded analytics snapshot.</returns>
    Task<JobExecutionAnalyticsSnapshot> GetExecutionAnalyticsAsync(
        string schedulerScopeKey,
        JobExecutionAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
