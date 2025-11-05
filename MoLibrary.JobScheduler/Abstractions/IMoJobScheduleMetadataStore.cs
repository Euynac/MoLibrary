using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
/// Abstraction for job metadata persistence layer.
/// Provides methods for managing job definitions, job instances, and job execution history.
/// Implementations can use in-memory storage, relational databases, NoSQL databases, or any other persistence mechanism.
/// </summary>
/// <remarks>
/// <para>
/// This interface separates the job scheduling logic from the underlying storage mechanism,
/// enabling flexibility in choosing appropriate storage based on deployment requirements:
/// </para>
/// <list type="bullet">
/// <item><description><b>In-Memory:</b> Fast, suitable for development and single-instance deployments (MetadataStoreInMemoryProvider)</description></item>
/// <item><description><b>SQL Databases:</b> ACID guarantees, suitable for distributed deployments requiring consistency</description></item>
/// <item><description><b>NoSQL Databases:</b> High scalability, suitable for high-volume job execution scenarios</description></item>
/// <item><description><b>Distributed Caches:</b> Low latency, suitable for distributed worker coordination</description></item>
/// </list>
/// <para>
/// All methods are asynchronous and accept an optional CancellationToken for cooperative cancellation.
/// Implementations should respect cancellation tokens and exit gracefully when cancellation is requested.
/// </para>
/// <para>
/// <b>Thread Safety:</b> Implementations must be thread-safe as they will be accessed concurrently
/// from multiple job scheduler and worker instances.
/// </para>
/// <para>
/// <b>Error Handling:</b> Implementations should throw meaningful exceptions for error scenarios
/// (e.g., JobNotFoundException, DuplicateJobKeyException, StorageUnavailableException).
/// </para>
/// </remarks>
public interface IMoJobScheduleMetadataStore
{
    #region Job Definitions

    /// <summary>
    /// Retrieves a job definition by its unique job key.
    /// </summary>
    /// <param name="jobKey">
    /// The unique identifier for the job (typically the job's TypeFullName).
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the JobDefinition if found, or null if no job with the specified key exists.
    /// </returns>
    /// <remarks>
    /// This method is called frequently by the job scheduler to retrieve job configuration
    /// before creating job instances. Implementations should consider caching to improve performance.
    /// </remarks>
    Task<JobDefinition?> GetJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all registered job definitions.
    /// </summary>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of all registered job definitions.
    /// Returns an empty collection if no jobs are registered.
    /// </returns>
    /// <remarks>
    /// This method is called during job scheduler initialization to load all job definitions
    /// and during job registration to check for existing jobs. The collection should include
    /// both recurring and triggered job definitions.
    /// </remarks>
    Task<IEnumerable<JobDefinition>> GetAllJobDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a job definition to the metadata store.
    /// Creates a new definition if it doesn't exist, or updates an existing definition if it does.
    /// </summary>
    /// <param name="definition">
    /// The job definition to persist. Must not be null.
    /// The JobKey property must be unique across all registered jobs.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method is called during job registration and when job configuration is updated via the API.
    /// Implementations should validate that the JobKey is unique before creating a new definition.
    /// For updates, the implementation should preserve the original registration timestamp if applicable.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
    /// <exception cref="ArgumentException">Thrown when definition.JobKey is null or empty.</exception>
    Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a job definition with the specified key exists in the metadata store.
    /// </summary>
    /// <param name="jobKey">
    /// The unique identifier for the job to check.
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result is true if a job definition with the specified key exists, false otherwise.
    /// </returns>
    /// <remarks>
    /// This method is used during job registration to prevent duplicate registrations.
    /// Implementations should provide an efficient check without retrieving the full job definition.
    /// </remarks>
    Task<bool> JobDefinitionExistsAsync(string jobKey, CancellationToken cancellationToken = default);

    #endregion

    #region Job Instances

    /// <summary>
    /// Retrieves a job instance by its unique instance identifier.
    /// </summary>
    /// <param name="instanceId">
    /// The unique identifier for the job instance (GUID).
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the JobInstance if found, or null if no instance with the specified ID exists.
    /// </returns>
    /// <remarks>
    /// This method is called by job executors to retrieve instance details during execution
    /// and by the API layer to check instance status. Implementations should support fast lookups by instance ID.
    /// </remarks>
    Task<JobInstance?> GetJobInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all job instances for a specific job key, optionally filtered by state.
    /// </summary>
    /// <param name="jobKey">
    /// The unique identifier for the job definition.
    /// Must not be null or empty.
    /// </param>
    /// <param name="stateFilter">
    /// Optional state filter to retrieve only instances in a specific state.
    /// If null, returns instances in all states.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of job instances matching the criteria.
    /// Returns an empty collection if no matching instances are found.
    /// </returns>
    /// <remarks>
    /// This method is used for querying job execution history and monitoring job status.
    /// Implementations should support efficient filtering by jobKey and state.
    /// Consider implementing pagination for large result sets.
    /// </remarks>
    Task<IEnumerable<JobInstance>> GetJobInstancesByKeyAsync(
        string jobKey,
        JobState? stateFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of job instances currently in Processing state for a specific job key.
    /// This is used to enforce concurrency limits.
    /// </summary>
    /// <param name="jobKey">
    /// The unique identifier for the job definition.
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the count of instances in Processing state for the specified job.
    /// Returns 0 if no instances are currently processing.
    /// </returns>
    /// <remarks>
    /// This method is called by the ConcurrencyGuard before starting job execution to check
    /// if the maximum concurrency limit has been reached. Performance is critical for this operation.
    /// Implementations should use efficient counting mechanisms (e.g., indexed queries, counters).
    /// </remarks>
    Task<int> GetProcessingCountAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a job instance to the metadata store.
    /// Creates a new instance if it doesn't exist, or updates an existing instance if it does.
    /// </summary>
    /// <param name="instance">
    /// The job instance to persist. Must not be null.
    /// The InstanceId property must be unique across all job instances.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method is called when creating new job instances and when updating instance state or metadata.
    /// Implementations should handle both insert and update operations efficiently.
    /// Consider using upsert operations if supported by the underlying storage.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    /// <exception cref="ArgumentException">Thrown when instance.InstanceId is null or empty.</exception>
    Task SaveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state of a job instance and optionally sets an error message.
    /// This is a specialized operation for efficient state transitions during job execution.
    /// </summary>
    /// <param name="instanceId">
    /// The unique identifier for the job instance.
    /// Must not be null or empty.
    /// </param>
    /// <param name="newState">
    /// The new state to transition to.
    /// Should be a valid state according to the job state machine.
    /// </param>
    /// <param name="errorMessage">
    /// Optional error message to store with the instance.
    /// Typically set when transitioning to Failed, Terminated, or Cancelled states.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method is called frequently during job execution to update state as jobs progress
    /// through their lifecycle (Enqueued → Processing → Succeeded/Failed).
    /// Implementations should:
    /// <list type="bullet">
    /// <item><description>Update the State property to newState</description></item>
    /// <item><description>Set ErrorMessage if provided</description></item>
    /// <item><description>Update timestamps (StartedAt when entering Processing, CompletedAt for terminal states)</description></item>
    /// <item><description>Perform the update atomically if possible</description></item>
    /// </list>
    /// Performance is critical as this is called on every state transition.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when instanceId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the instance is not found.</exception>
    Task UpdateJobStateAsync(
        string instanceId,
        JobState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Job History

    /// <summary>
    /// Archives a completed job instance to the job history storage.
    /// This is typically called when a job reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    /// <param name="instance">
    /// The job instance to archive. Must not be null.
    /// Should be in a terminal state.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// Implementations may choose to:
    /// <list type="bullet">
    /// <item><description>Move the instance to a separate history table/collection for better query performance</description></item>
    /// <item><description>Keep the instance in the same storage but mark it as archived</description></item>
    /// <item><description>Compress or summarize instance data to reduce storage costs</description></item>
    /// </list>
    /// This method supports the requirement for maintaining historical records of job executions
    /// for auditing, reporting, and troubleshooting purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    Task ArchiveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated historical job instances for a specific job key.
    /// Used for displaying job execution history in the UI and generating reports.
    /// </summary>
    /// <param name="jobKey">
    /// The unique identifier for the job definition.
    /// Must not be null or empty.
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of instances to return per page.
    /// Must be greater than 0.
    /// </param>
    /// <param name="pageNumber">
    /// The page number to retrieve (1-based).
    /// Must be greater than 0.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of historical job instances for the specified page.
    /// Returns an empty collection if no history exists for the specified page.
    /// Instances should be ordered by creation time descending (most recent first).
    /// </returns>
    /// <remarks>
    /// This method supports querying job execution history with pagination to handle large datasets efficiently.
    /// Implementations should:
    /// <list type="bullet">
    /// <item><description>Order results by CreatedAt descending (most recent first)</description></item>
    /// <item><description>Support efficient pagination using skip/take or cursor-based pagination</description></item>
    /// <item><description>Include both archived and non-archived instances</description></item>
    /// <item><description>Filter by jobKey to retrieve history for a specific job</description></item>
    /// </list>
    /// Consider adding indices on jobKey and CreatedAt for optimal query performance.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when jobKey is null or empty, or pageSize/pageNumber are invalid.</exception>
    Task<IEnumerable<JobInstance>> GetJobHistoryAsync(
        string jobKey,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default);

    #endregion
}
