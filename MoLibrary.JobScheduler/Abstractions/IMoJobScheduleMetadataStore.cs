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
    Task<IEnumerable<JobDefinition>> GetAllJobDefinitionsAsync(bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a job definition to the metadata store (creates or updates).
    /// </summary>
    /// <param name="definition">The job definition to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
    /// <exception cref="ArgumentException">Thrown when definition.JobKey is null or empty.</exception>
    Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a job definition with the specified key exists.
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job definition exists, otherwise false.</returns>
    Task<bool> JobDefinitionExistsAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a job definition by marking it as deleted without physically removing it from the store.
    /// Sets IsDeleted = true and DeletedAt = current timestamp.
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job to soft delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when jobKey is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the job definition is not found.</exception>
    Task SoftDeleteJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default);

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
    Task<IEnumerable<JobInstance>> GetJobInstancesByKeyAsync(
        string jobKey,
        JobState? stateFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of job instances currently in Processing state for a specific job key.
    /// Used to enforce concurrency limits.
    /// </summary>
    /// <param name="jobKey">The unique identifier for the job definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of instances in Processing state. Returns 0 if none are processing.</returns>
    Task<int> GetProcessingCountAsync(string jobKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a job instance to the metadata store (creates or updates).
    /// </summary>
    /// <param name="instance">The job instance to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    /// <exception cref="ArgumentException">Thrown when instance.InstanceId is null or empty.</exception>
    Task SaveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state of a job instance and optionally sets an error message.
    /// Updates timestamps (StartedAt when entering Processing, CompletedAt for terminal states).
    /// </summary>
    /// <param name="instanceId">The unique identifier for the job instance.</param>
    /// <param name="newState">The new state to transition to.</param>
    /// <param name="errorMessage">Optional error message (typically for Failed, Terminated, or Cancelled states).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
    /// Called when a job reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    /// <param name="instance">The job instance to archive (should be in a terminal state).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    Task ArchiveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default);

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
}
