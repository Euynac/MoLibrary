using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Operations;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Provides the single durable correctness boundary for catalog and execution state.
/// </summary>
public interface IJobSchedulerStore : IJobCatalogStore, IJobExecutionStore, IJobSchedulerAnalyticsStore
{
    /// <summary>
    /// Gets the operational projection for one exact active logical job key.
    /// </summary>
    /// <param name="schedulerScopeKey">The scheduler scope containing the active catalog.</param>
    /// <param name="jobKey">The globally unique logical job key.</param>
    /// <param name="cancellationToken">Cancels the read operation.</param>
    /// <returns>
    /// The bounded operational projection, or <see langword="null"/> when the key is not present in the active catalog.
    /// </returns>
    Task<JobOperationalSummary?> GetOperationalSummaryAsync(
        string schedulerScopeKey,
        string jobKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries a page of active definitions enriched with their recurring cursor, newest execution, active queue,
    /// and exact-revision worker capability signals.
    /// </summary>
    /// <remarks>
    /// Catalog filters and ordering are applied before runtime enrichment, keeping execution and worker reads bounded
    /// to the selected definition page. Returned execution snapshots omit retained audit history.
    /// </remarks>
    /// <param name="schedulerScopeKey">The scheduler scope whose active jobs are queried.</param>
    /// <param name="query">Catalog filters and page bounds.</param>
    /// <param name="cancellationToken">Cancels the read operation.</param>
    /// <returns>A page ordered by logical job key and the total number of matching active definitions.</returns>
    Task<QueryResult<JobOperationalSummary>> QueryOperationalSummariesAsync(
        string schedulerScopeKey,
        JobCatalogQuery query,
        CancellationToken cancellationToken = default);
}
