using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.Abstractions;

/// <summary>
/// Abstraction for task metadata persistence layer.
/// Provides methods for managing task definitions, task instances, and task execution history.
/// Implementations can use in-memory storage, relational databases, NoSQL databases, or any other persistence mechanism.
/// </summary>
/// <remarks>
/// <para>
/// This interface separates the task scheduling logic from the underlying storage mechanism,
/// enabling flexibility in choosing appropriate storage based on deployment requirements:
/// </para>
/// <list type="bullet">
/// <item><description><b>In-Memory:</b> Fast, suitable for development and single-instance deployments (MetadataStoreInMemoryProvider)</description></item>
/// <item><description><b>SQL Databases:</b> ACID guarantees, suitable for distributed deployments requiring consistency</description></item>
/// <item><description><b>NoSQL Databases:</b> High scalability, suitable for high-volume task execution scenarios</description></item>
/// <item><description><b>Distributed Caches:</b> Low latency, suitable for distributed worker coordination</description></item>
/// </list>
/// <para>
/// All methods are asynchronous and accept an optional CancellationToken for cooperative cancellation.
/// Implementations should respect cancellation tokens and exit gracefully when cancellation is requested.
/// </para>
/// <para>
/// <b>Thread Safety:</b> Implementations must be thread-safe as they will be accessed concurrently
/// from multiple task scheduler and worker instances.
/// </para>
/// <para>
/// <b>Error Handling:</b> Implementations should throw meaningful exceptions for error scenarios
/// (e.g., TaskNotFoundException, DuplicateTaskKeyException, StorageUnavailableException).
/// </para>
/// </remarks>
public interface IMoTaskScheduleMetadataStore
{
    #region Task Definitions

    /// <summary>
    /// Retrieves a task definition by its unique task key.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task (typically the task's TypeFullName).
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the TaskDefinition if found, or null if no task with the specified key exists.
    /// </returns>
    /// <remarks>
    /// This method is called frequently by the task scheduler to retrieve task configuration
    /// before creating task instances. Implementations should consider caching to improve performance.
    /// </remarks>
    Task<TaskDefinition?> GetTaskDefinitionAsync(string taskKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all registered task definitions.
    /// </summary>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of all registered task definitions.
    /// Returns an empty collection if no tasks are registered.
    /// </returns>
    /// <remarks>
    /// This method is called during task scheduler initialization to load all task definitions
    /// and during task registration to check for existing tasks. The collection should include
    /// both recurring and triggered task definitions.
    /// </remarks>
    Task<IEnumerable<TaskDefinition>> GetAllTaskDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a task definition to the metadata store.
    /// Creates a new definition if it doesn't exist, or updates an existing definition if it does.
    /// </summary>
    /// <param name="definition">
    /// The task definition to persist. Must not be null.
    /// The TaskKey property must be unique across all registered tasks.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method is called during task registration and when task configuration is updated via the API.
    /// Implementations should validate that the TaskKey is unique before creating a new definition.
    /// For updates, the implementation should preserve the original registration timestamp if applicable.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when definition is null.</exception>
    /// <exception cref="ArgumentException">Thrown when definition.TaskKey is null or empty.</exception>
    Task SaveTaskDefinitionAsync(TaskDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a task definition with the specified key exists in the metadata store.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task to check.
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result is true if a task definition with the specified key exists, false otherwise.
    /// </returns>
    /// <remarks>
    /// This method is used during task registration to prevent duplicate registrations.
    /// Implementations should provide an efficient check without retrieving the full task definition.
    /// </remarks>
    Task<bool> TaskDefinitionExistsAsync(string taskKey, CancellationToken cancellationToken = default);

    #endregion

    #region Task Instances

    /// <summary>
    /// Retrieves a task instance by its unique instance identifier.
    /// </summary>
    /// <param name="instanceId">
    /// The unique identifier for the task instance (GUID).
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the TaskInstance if found, or null if no instance with the specified ID exists.
    /// </returns>
    /// <remarks>
    /// This method is called by task executors to retrieve instance details during execution
    /// and by the API layer to check instance status. Implementations should support fast lookups by instance ID.
    /// </remarks>
    Task<TaskInstance?> GetTaskInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all task instances for a specific task key, optionally filtered by state.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task definition.
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
    /// The task result contains a collection of task instances matching the criteria.
    /// Returns an empty collection if no matching instances are found.
    /// </returns>
    /// <remarks>
    /// This method is used for querying task execution history and monitoring task status.
    /// Implementations should support efficient filtering by taskKey and state.
    /// Consider implementing pagination for large result sets.
    /// </remarks>
    Task<IEnumerable<TaskInstance>> GetTaskInstancesByKeyAsync(
        string taskKey,
        TaskState? stateFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of task instances currently in Processing state for a specific task key.
    /// This is used to enforce concurrency limits.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task definition.
    /// Must not be null or empty.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the count of instances in Processing state for the specified task.
    /// Returns 0 if no instances are currently processing.
    /// </returns>
    /// <remarks>
    /// This method is called by the ConcurrencyGuard before starting task execution to check
    /// if the maximum concurrency limit has been reached. Performance is critical for this operation.
    /// Implementations should use efficient counting mechanisms (e.g., indexed queries, counters).
    /// </remarks>
    Task<int> GetProcessingCountAsync(string taskKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a task instance to the metadata store.
    /// Creates a new instance if it doesn't exist, or updates an existing instance if it does.
    /// </summary>
    /// <param name="instance">
    /// The task instance to persist. Must not be null.
    /// The InstanceId property must be unique across all task instances.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This method is called when creating new task instances and when updating instance state or metadata.
    /// Implementations should handle both insert and update operations efficiently.
    /// Consider using upsert operations if supported by the underlying storage.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    /// <exception cref="ArgumentException">Thrown when instance.InstanceId is null or empty.</exception>
    Task SaveTaskInstanceAsync(TaskInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the state of a task instance and optionally sets an error message.
    /// This is a specialized operation for efficient state transitions during task execution.
    /// </summary>
    /// <param name="instanceId">
    /// The unique identifier for the task instance.
    /// Must not be null or empty.
    /// </param>
    /// <param name="newState">
    /// The new state to transition to.
    /// Should be a valid state according to the task state machine.
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
    /// This method is called frequently during task execution to update state as tasks progress
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
    Task UpdateTaskStateAsync(
        string instanceId,
        TaskState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Task History

    /// <summary>
    /// Archives a completed task instance to the task history storage.
    /// This is typically called when a task reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    /// <param name="instance">
    /// The task instance to archive. Must not be null.
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
    /// This method supports the requirement for maintaining historical records of task executions
    /// for auditing, reporting, and troubleshooting purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when instance is null.</exception>
    Task ArchiveTaskInstanceAsync(TaskInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated historical task instances for a specific task key.
    /// Used for displaying task execution history in the UI and generating reports.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task definition.
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
    /// The task result contains a collection of historical task instances for the specified page.
    /// Returns an empty collection if no history exists for the specified page.
    /// Instances should be ordered by creation time descending (most recent first).
    /// </returns>
    /// <remarks>
    /// This method supports querying task execution history with pagination to handle large datasets efficiently.
    /// Implementations should:
    /// <list type="bullet">
    /// <item><description>Order results by CreatedAt descending (most recent first)</description></item>
    /// <item><description>Support efficient pagination using skip/take or cursor-based pagination</description></item>
    /// <item><description>Include both archived and non-archived instances</description></item>
    /// <item><description>Filter by taskKey to retrieve history for a specific task</description></item>
    /// </list>
    /// Consider adding indices on taskKey and CreatedAt for optimal query performance.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when taskKey is null or empty, or pageSize/pageNumber are invalid.</exception>
    Task<IEnumerable<TaskInstance>> GetTaskHistoryAsync(
        string taskKey,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default);

    #endregion
}
