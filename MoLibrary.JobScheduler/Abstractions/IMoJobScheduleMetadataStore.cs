using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Abstractions;

/// <summary>
/// Abstraction for job metadata persistence layer.
/// Manages job definitions, instances, and execution history.
/// Implementations must be thread-safe and support various storage backends (in-memory, SQL, NoSQL, etc.).
/// </summary>
public interface IMoJobScheduleMetadataStore
{
    #region Job Definitions

    /// <summary>
    /// Retrieves a job definition by its unique job key.
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job (typically TypeFullName).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JobDefinition if found, otherwise null.</returns>
    Task<JobDefinition?> GetJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all registered job definitions.
    /// </summary>
    /// <param name="includeDeleted">Whether to include soft-deleted job definitions. Default is false (excludes deleted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of all registered job definitions.</returns>
    Task<List<JobDefinition>> GetAllJobDefinitionsAsync(bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a job definition to the metadata store (creates or updates).
    /// </summary>
    /// <param name="definition">The job definition to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
    /// <exception cref="ArgumentException">Thrown when definition.JobKey is null or empty.</exception>
    Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default);
    #endregion

    #region Job Instances

    /// <summary>
    /// Retrieves a job instance by its unique instance identifier.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the job instance (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JobInstance if found, otherwise null.</returns>
    Task<JobInstance?> GetJobInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all job instances for a specific job key, optionally filtered by state.
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job definition.</param>
    /// <param name="stateFilter">Optional state filter. If null, returns instances in all states.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of job instances matching the criteria.</returns>
    Task<List<JobInstance>> GetJobInstancesByKeyAsync(string jobKey,
        JobState? stateFilter = null,
        CancellationToken cancellationToken = default);
    

    /// <summary>
    /// Persists a job instance to the metadata store (creates or updates).
    /// </summary>
    /// <param name="instance">The job instance to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    /// <exception cref="ArgumentException">Thrown when instance.InstanceId is null or empty.</exception>
    Task SaveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);
    #endregion

    #region Job History
    /// <summary>
    /// Retrieves paginated historical job instances for a specific job key.
    /// Results are ordered by CreatedAt descending (most recent first).
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job definition.</param>
    /// <param name="pageSize">The maximum number of instances to return per page (must be > 0).</param>
    /// <param name="pageNumber">The page number to retrieve, 1-based (must be > 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of historical job instances for the specified page, ordered by CreatedAt descending.</returns>
    /// <exception cref="ArgumentException">Thrown when jobKey is null or empty, or pageSize/pageNumber are invalid.</exception>
    Task<IEnumerable<JobInstance>> GetJobHistoryAsync(
        string jobKey,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default);

    #endregion

    #region Advanced Queries for UI

    /// <summary>
    /// Retrieves job instances with advanced filtering and pagination.
    /// Results are ordered by CreatedAt descending (most recent first).
    /// </summary>
    /// <param name="jobKey">Optional job key filter (fuzzy match).</param>
    /// <param name="stateFilter">Optional state filter.</param>
    /// <param name="startTime">Optional start time filter (inclusive).</param>
    /// <param name="endTime">Optional end time filter (inclusive).</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of job instances matching the criteria.</returns>
    Task<List<JobInstance>> GetJobInstancesAsync(
        string? jobKey = null,
        JobState? stateFilter = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of job instances matching the filter criteria.
    /// Used for pagination calculations.
    /// </summary>
    /// <param name="jobKey">Optional job key filter (fuzzy match).</param>
    /// <param name="stateFilter">Optional state filter.</param>
    /// <param name="startTime">Optional start time filter (inclusive).</param>
    /// <param name="endTime">Optional end time filter (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total count of matching instances.</returns>
    Task<int> GetJobInstancesCountAsync(
        string? jobKey = null,
        JobState? stateFilter = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    #endregion
}
