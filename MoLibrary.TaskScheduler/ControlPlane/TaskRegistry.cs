using Microsoft.Extensions.Logging;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.Exceptions;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.ControlPlane;

/// <summary>
/// Manages task definition registration and retrieval.
/// Provides methods for registering new task definitions, checking registration status,
/// and retrieving task metadata from the metadata store.
/// </summary>
/// <remarks>
/// <para>
/// TaskRegistry acts as the central coordinator for task definition management in the task scheduler system.
/// It ensures task definitions are properly validated and persisted before tasks can be scheduled or executed.
/// </para>
/// <para>
/// <b>Registration Process:</b>
/// </para>
/// <list type="number">
/// <item><description>Module initialization discovers task types during startup</description></item>
/// <item><description>Task keys are checked against the registry to identify unregistered tasks</description></item>
/// <item><description>Task definitions are created from task metadata (attributes, reflection)</description></item>
/// <item><description>Registry validates uniqueness and persists definitions to metadata store</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b>
/// The registry delegates persistence to IMoTaskScheduleMetadataStore, which must provide thread-safe operations.
/// Multiple workers can safely call GetDefinitionAsync concurrently.
/// </para>
/// </remarks>
public class TaskRegistry(
    IMoTaskScheduleMetadataStore metadataStore,
    ILogger<TaskRegistry> logger)
{
    /// <summary>
    /// Checks which task keys from the provided collection are not yet registered in the metadata store.
    /// </summary>
    /// <param name="taskKeys">
    /// Collection of task keys to check for registration status.
    /// Task keys are typically the full type name of the task class.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of task keys that are not yet registered.
    /// Returns an empty collection if all provided task keys are already registered.
    /// </returns>
    /// <remarks>
    /// This method is used during module initialization to identify which tasks need to be registered.
    /// It performs efficient existence checks without loading full task definitions.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when taskKeys is null.</exception>
    /// <example>
    /// <code>
    /// var discoveredKeys = new[] { "MyApp.Tasks.EmailTask", "MyApp.Tasks.ReportTask" };
    /// var unregistered = await taskRegistry.CheckUnregisteredAsync(discoveredKeys);
    /// // unregistered contains only keys that need to be registered
    /// </code>
    /// </example>
    public async Task<IEnumerable<string>> CheckUnregisteredAsync(
        IEnumerable<string> taskKeys,
        CancellationToken cancellationToken = default)
    {
        if (taskKeys == null)
        {
            throw new ArgumentNullException(nameof(taskKeys));
        }

        var taskKeysList = taskKeys.ToList();
        var unregistered = new List<string>();

        foreach (var taskKey in taskKeysList)
        {
            if (string.IsNullOrWhiteSpace(taskKey))
            {
                logger.LogWarning("Skipping empty or null task key during registration check");
                continue;
            }

            var exists = await metadataStore.TaskDefinitionExistsAsync(taskKey, cancellationToken);
            if (!exists)
            {
                unregistered.Add(taskKey);
            }
        }

        logger.LogDebug(
            "Registration check completed: {TotalCount} keys checked, {UnregisteredCount} unregistered",
            taskKeysList.Count,
            unregistered.Count);

        return unregistered;
    }

    /// <summary>
    /// Registers multiple task definitions in the metadata store.
    /// Validates that all task keys are unique and throws an exception if duplicates are detected.
    /// </summary>
    /// <param name="definitions">
    /// Collection of task definitions to register.
    /// Each definition must have a unique TaskKey.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs the following validations:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Checks for duplicate TaskKeys within the provided collection</description></item>
    /// <item><description>Checks for conflicts with existing registered tasks</description></item>
    /// <item><description>Logs all successful registrations for audit purposes</description></item>
    /// </list>
    /// <para>
    /// If any validation fails, the operation throws TaskRegistrationException with details about the conflict.
    /// The exception includes the conflicting TaskKey for troubleshooting.
    /// </para>
    /// <para>
    /// <b>Important:</b> This method does not cache task definitions. All subsequent lookups
    /// will query the metadata store directly to ensure consistency in distributed deployments.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when definitions is null.</exception>
    /// <exception cref="TaskRegistrationException">
    /// Thrown when duplicate task keys are detected within the collection or when
    /// attempting to register a task that already exists in the metadata store.
    /// </exception>
    /// <example>
    /// <code>
    /// var definitions = new[]
    /// {
    ///     new TaskDefinition
    ///     {
    ///         TaskKey = "MyApp.Tasks.EmailTask",
    ///         TaskName = "Email Sender",
    ///         Type = TaskType.Triggered,
    ///         MaxConcurrency = 5
    ///     }
    /// };
    ///
    /// try
    /// {
    ///     await taskRegistry.RegisterTasksAsync(definitions);
    /// }
    /// catch (TaskRegistrationException ex)
    /// {
    ///     Console.WriteLine($"Registration failed for task: {ex.TaskKey}");
    /// }
    /// </code>
    /// </example>
    public async Task RegisterTasksAsync(
        IEnumerable<TaskDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        var definitionsList = definitions.ToList();

        // Check for duplicate keys within the provided collection
        var duplicates = definitionsList
            .GroupBy(d => d.TaskKey)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            var duplicateKeys = string.Join(", ", duplicates);
            var message = $"Duplicate task keys detected in registration batch: {duplicateKeys}";

            logger.LogError(message);
            throw new TaskRegistrationException(message, duplicates.First());
        }

        // Check for conflicts with existing registered tasks
        foreach (var definition in definitionsList)
        {
            if (string.IsNullOrWhiteSpace(definition.TaskKey))
            {
                logger.LogError("Attempted to register task with null or empty TaskKey: {TaskName}", definition.TaskName);
                throw new TaskRegistrationException("Task definition must have a non-empty TaskKey");
            }

            var exists = await metadataStore.TaskDefinitionExistsAsync(definition.TaskKey, cancellationToken);
            if (exists)
            {
                var message = $"Task with key '{definition.TaskKey}' is already registered. " +
                             $"Cannot register duplicate task definitions.";

                logger.LogError(
                    "Duplicate task registration attempt: {TaskKey} ({TaskName})",
                    definition.TaskKey,
                    definition.TaskName);

                throw new TaskRegistrationException(message, definition.TaskKey);
            }
        }

        // Register all definitions
        foreach (var definition in definitionsList)
        {
            await metadataStore.SaveTaskDefinitionAsync(definition, cancellationToken);

            logger.LogInformation(
                "Task registered: {TaskKey} ({TaskName}), Type: {TaskType}, MaxConcurrency: {MaxConcurrency}, RetryCount: {RetryCount}",
                definition.TaskKey,
                definition.TaskName,
                definition.Type,
                definition.MaxConcurrency,
                definition.RetryCount);
        }

        logger.LogInformation(
            "Successfully registered {Count} task definition(s)",
            definitionsList.Count);
    }

    /// <summary>
    /// Retrieves a task definition by its unique task key.
    /// </summary>
    /// <param name="taskKey">
    /// The unique identifier for the task.
    /// Typically the full type name of the task class.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the TaskDefinition if found, or null if no task with the specified key exists.
    /// </returns>
    /// <remarks>
    /// This method does not cache task definitions and always queries the metadata store.
    /// This ensures consistency in distributed deployments where task definitions may be updated
    /// via the Control Plane API.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when taskKey is null or empty.</exception>
    /// <example>
    /// <code>
    /// var definition = await taskRegistry.GetDefinitionAsync("MyApp.Tasks.EmailTask");
    /// if (definition != null)
    /// {
    ///     Console.WriteLine($"Task: {definition.TaskName}");
    ///     Console.WriteLine($"Max Concurrency: {definition.MaxConcurrency}");
    /// }
    /// </code>
    /// </example>
    public async Task<TaskDefinition?> GetDefinitionAsync(
        string taskKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
        }

        var definition = await metadataStore.GetTaskDefinitionAsync(taskKey, cancellationToken);

        if (definition == null)
        {
            logger.LogWarning("Task definition not found: {TaskKey}", taskKey);
        }

        return definition;
    }
}
