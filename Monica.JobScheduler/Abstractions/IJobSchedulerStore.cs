using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Provides the single durable correctness boundary for definition and execution state.
/// </summary>
public interface IJobSchedulerStore : IJobDefinitionStore, IJobExecutionStore, IJobSchedulerAnalyticsStore
{
    /// <summary>
    /// Gets the operational projection for one exact job definition.
    /// </summary>
    /// <returns>
    /// The bounded operational projection, or <see langword="null"/> when the identity is unknown.
    /// </returns>
    Task<JobOperationalSummary?> GetOperationalSummaryAsync(
        string schedulerScopeKey,
        JobId jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries a page of definitions enriched with their recurring cursor, newest execution, and active queue counts.
    /// </summary>
    /// <remarks>
    /// Definition filters and ordering are applied before runtime enrichment, keeping execution and cursor reads
    /// bounded to the selected definition page. Returned execution snapshots omit retained audit history.
    /// </remarks>
    Task<QueryResult<JobOperationalSummary>> QueryOperationalSummariesAsync(
        string schedulerScopeKey,
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default);
}
