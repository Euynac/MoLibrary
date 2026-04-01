using System.Linq.Expressions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Job metadata warehousing interface, providing persistence and query capabilities of Job definitions and instances
/// </summary>
/// <remarks>
/// Implementation must be thread-safe and support multiple storage backends (in-memory, SQL, NoSQL, etc.)
/// </remarks>
public interface IJobMetadataRepository
{
    #region JobDefinition Operations

    /// <summary>
    /// Get a single Job definition based on JobKey
    /// </summary>
    /// <param name="jobKey">Job unique identifier (usually the full name of the type)</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Returns JobDefinition if found, otherwise returns null</returns>
    Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save Job Definition (Create or Update)
    /// </summary>
    /// <param name="definition">Job definition to save</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <exception cref="ArgumentNullException">definition is null</exception>
    /// <exception cref="ArgumentException">definition.JobKey is null or empty</exception>
    Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query Job definition list
    /// </summary>
    /// <param name="query">Query conditions</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Query results, including list of items and total number</returns>
    Task<QueryResult<JobDefinition>> QueryDefinitionsAsync(
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default);

    #endregion

    #region JobInstance Operations

    /// <summary>
    /// Get a single Job instance based on instance ID
    /// </summary>
    /// <param name="instanceId">Instance unique identifier (GUID)</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Returns JobInstance if found, otherwise returns null</returns>
    Task<JobInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save Job instance (create or update)
    /// </summary>
    /// <param name="instance">Job instance to save</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <exception cref="ArgumentNullException">instance is null</exception>
    /// <exception cref="ArgumentException">instance.InstanceId is null or empty</exception>
    Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query Job instance list
    /// </summary>
    /// <param name="query">Query conditions</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Query results, including list of items and total number</returns>
    Task<QueryResult<JobInstance>> QueryInstancesAsync(
        JobInstanceQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query the Job instance list and project it to a custom type (supports database-side SELECT projection)
    /// </summary>
    /// <typeparam name="TResult">Projection result type</typeparam>
    /// <param name="query">Query conditions</param>
    /// <param name="selector">Projection expression, based on JobInstance property</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Query results, including projected item list and total number</returns>
    /// <remarks>
    /// For EF Core implementations, projection is performed on the database side (generating the corresponding SELECT statement)
    /// Supported attributes: InstanceId, JobKey, State, CreatedAt, StartedAt, CompletedAt, RetryAttempt, etc.
    /// </remarks>
    Task<QueryResult<TResult>> QueryInstancesAsync<TResult>(
        JobInstanceQuery query,
        Expression<Func<JobInstance, TResult>> selector,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the number of instance statistics in each status within a specified time range (using database-side GROUP BY)
    /// </summary>
    /// <param name="startTime">Starting time (optional)</param>
    /// <param name="endTime">End time (optional)</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>A dictionary of the number of instances corresponding to each state</returns>
    Task<Dictionary<JobState, int>> GetStateStatisticsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtain the last execution instances of multiple jobs in batches (optimizing the N+1 query problem)
    /// </summary>
    /// <param name="jobKeys">Job key collection</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Dictionary, Key is JobKey, Value is the last execution instance (null if none)</returns>
    Task<Dictionary<string, JobInstance?>> GetLatestInstancesAsync(
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Job instances in batches
    /// </summary>
    /// <param name="instanceIds">The set of instance IDs to delete</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>Number of successfully deleted instances</returns>
    /// <remarks>
    /// Implementations should handle partial failures gracefully and non-existent instances should be silently ignored
    /// </remarks>
    Task<int> DeleteInstancesAsync(
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query the list of instance IDs that need to be cleaned (optimized batch cleaning query)
    /// </summary>
    /// <param name="retentionPolicies">Job retention policy dictionary (JobKey -> (MaxRecords, MaxDays))</param>
    /// <param name="maxRetainedOrphanedInstances">Maximum number of records to keep for orphaned instances (default 10)</param>
    /// <param name="maxDeletionsPerCycle">Maximum number of deletions per cleanup (0 = no limit)</param>
    /// <param name="cancellationToken">cancel token</param>
    /// <returns>List of instance IDs to be deleted</returns>
    /// <remarks>
    /// Implementations should accomplish the following filtering at the database level:
    /// 1. Only consider final state instances (Succeeded, Terminated, Canceled, Skipped, Failed)
    /// 2. For each JobKey, keep the most recent N records (N=MaxRecords)
    /// 3. Delete records older than MaxDays days
    /// 4. Orphaned instances (JobKey is not in retentionPolicies) retain the most recent maxRetainedOrphanedInstances entries
    /// </remarks>
    Task<List<string>> GetCleanupCandidatesAsync(
        IReadOnlyDictionary<string, (int MaxRecords, int? MaxDays)> retentionPolicies,
        int maxRetainedOrphanedInstances = 10,
        int maxDeletionsPerCycle = 0,
        CancellationToken cancellationToken = default);

    #endregion
}
