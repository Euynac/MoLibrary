using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// High-performance in-memory cache layer for JobDefinition management.
/// Provides write-through caching with lazy invalidation based on JobDefinitionsChangedEvent.
/// Thread-safe for concurrent access.
/// </summary>
public interface IJobDefinitionCacheService
{
    /// <summary>
    /// Gets a job definition by key. Checks for invalidation in StateStore, reloads if needed.
    /// Returns from cache if still valid.
    /// </summary>
    /// <param name="jobKey">The unique job key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The job definition, or null if not found</returns>
    Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all cached job definitions. Returns current cache snapshot without invalidation checks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Read-only list of all cached job definitions</returns>
    Task<IReadOnlyList<JobDefinition>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a job definition with write-through semantics.
    /// Immediately persists to metadata store, updates cache, and marks as updated in StateStore.
    /// Optionally publishes JobDefinitionsChangedEvent to notify schedulers of the update.
    /// </summary>
    /// <param name="definition">The job definition to save</param>
    /// <param name="publishChangeEvent">Whether to publish JobDefinitionsChangedEvent (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveDefinitionAsync(
        JobDefinition definition,
        bool publishChangeEvent = false,
        CancellationToken cancellationToken = default);

}
