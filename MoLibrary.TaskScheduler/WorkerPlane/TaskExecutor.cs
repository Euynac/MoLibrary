using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.Events;
using MoLibrary.TaskScheduler.Models;
using MoLibrary.TaskScheduler.Tasks;

namespace MoLibrary.TaskScheduler.WorkerPlane;

/// <summary>
/// Executes individual task instances with dependency injection, timeout handling, and retry logic.
/// Manages the complete task execution lifecycle including DI resolution, distributed cancellation,
/// state updates, and cleanup.
/// </summary>
/// <remarks>
/// <para>
/// TaskExecutor is responsible for the actual execution of task instances. It:
/// </para>
/// <list type="bullet">
/// <item><description>Creates scoped service providers for isolated task execution</description></item>
/// <item><description>Resolves task instances via dependency injection</description></item>
/// <item><description>Manages distributed cancellation tokens via IMoCancellationManager</description></item>
/// <item><description>Enforces timeouts using Task.WhenAny pattern</description></item>
/// <item><description>Implements retry logic for failed tasks</description></item>
/// <item><description>Updates task state throughout the execution lifecycle</description></item>
/// <item><description>Ensures proper cleanup of cancellation tokens</description></item>
/// </list>
/// </remarks>
public class TaskExecutor(
    IServiceProvider serviceProvider,
    IMoTaskScheduleMetadataStore metadataStore,
    IMoCancellationManager cancellationManager,
    IMoEventBus eventBus,
    ILogger<TaskExecutor> logger)
{
    /// <summary>
    /// Executes a task instance with full lifecycle management.
    /// </summary>
    /// <param name="instance">The task instance to execute.</param>
    /// <param name="definition">The task definition containing configuration and metadata.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    /// <returns>A task representing the execution operation.</returns>
    public async Task ExecuteAsync(
        TaskInstance instance,
        TaskDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        logger.LogInformation(
            "Starting execution of task {TaskKey}, InstanceId: {InstanceId}",
            instance.TaskKey,
            instance.InstanceId);

        CancellationToken taskCancellationToken = default;
        IServiceScope? scope = null;

        try
        {
            // Step 1: Create scoped service provider for task execution
            scope = serviceProvider.CreateScope();

            // Step 2: Get or create distributed cancellation token
            taskCancellationToken = await cancellationManager.GetOrCreateTokenAsync(
                instance.InstanceId,
                cancellationToken);

            logger.LogDebug(
                "Acquired cancellation token for instance {InstanceId}",
                instance.InstanceId);

            // Step 3: Update state to Processing
            await metadataStore.UpdateTaskStateAsync(
                instance.InstanceId,
                TaskState.Processing,
                null,
                cancellationToken);

            instance.State = TaskState.Processing;
            instance.StartedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Task {TaskKey} instance {InstanceId} state updated to Processing",
                instance.TaskKey,
                instance.InstanceId);

            // Step 4: Resolve and execute task
            var executionTask = ResolveAndExecuteTaskAsync(
                scope.ServiceProvider,
                definition,
                instance,
                taskCancellationToken);

            // Step 5: Enforce timeout using Task.WhenAny
            var timeoutTask = Task.Delay(definition.MaxExecutionTimeout, cancellationToken);
            var completedTask = await Task.WhenAny(executionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                logger.LogWarning(
                    "Task {TaskKey} instance {InstanceId} exceeded timeout of {Timeout}",
                    instance.TaskKey,
                    instance.InstanceId,
                    definition.MaxExecutionTimeout);

                // Cancel the distributed token (propagates to all workers if distributed)
                await cancellationManager.CancelTokenAsync(instance.InstanceId, cancellationToken);

                // Wait a brief moment for graceful cancellation
                await Task.WhenAny(executionTask, Task.Delay(TimeSpan.FromSeconds(2)));

                // Update state to Failed
                await UpdateTaskStateAsync(
                    instance,
                    TaskState.Failed,
                    $"Execution timeout after {definition.MaxExecutionTimeout}",
                    cancellationToken);

                // Check for retry
                await HandleRetryLogicAsync(instance, definition, cancellationToken);
            }
            else
            {
                // Task completed (either successfully or with exception)
                try
                {
                    await executionTask; // Rethrow any exception

                    // Success
                    logger.LogInformation(
                        "Task {TaskKey} instance {InstanceId} completed successfully",
                        instance.TaskKey,
                        instance.InstanceId);

                    await UpdateTaskStateAsync(
                        instance,
                        TaskState.Succeeded,
                        null,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (taskCancellationToken.IsCancellationRequested)
                {
                    // Task was cancelled via distributed cancellation (manual or timeout)
                    logger.LogWarning(
                        "Task {TaskKey} instance {InstanceId} was cancelled",
                        instance.TaskKey,
                        instance.InstanceId);

                    await UpdateTaskStateAsync(
                        instance,
                        TaskState.Cancelled,
                        "Task execution was cancelled",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    // Task threw an exception
                    logger.LogError(
                        ex,
                        "Task {TaskKey} instance {InstanceId} failed with exception: {Message}",
                        instance.TaskKey,
                        instance.InstanceId,
                        ex.Message);

                    await UpdateTaskStateAsync(
                        instance,
                        TaskState.Failed,
                        $"{ex.GetType().Name}: {ex.Message}",
                        cancellationToken);

                    // Check for retry
                    await HandleRetryLogicAsync(instance, definition, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any unexpected errors in the executor itself
            logger.LogError(
                ex,
                "Critical error in TaskExecutor for {TaskKey} instance {InstanceId}: {Message}",
                instance.TaskKey,
                instance.InstanceId,
                ex.Message);

            try
            {
                await UpdateTaskStateAsync(
                    instance,
                    TaskState.Failed,
                    $"Executor error: {ex.GetType().Name}: {ex.Message}",
                    cancellationToken);
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update task state after executor error: {Message}",
                    updateEx.Message);
            }
        }
        finally
        {
            // Step 10: Always cleanup cancellation token
            try
            {
                await cancellationManager.DeleteTokenAsync(instance.InstanceId, CancellationToken.None);
                logger.LogDebug(
                    "Cleaned up cancellation token for instance {InstanceId}",
                    instance.InstanceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to cleanup cancellation token for instance {InstanceId}: {Message}",
                    instance.InstanceId,
                    ex.Message);
            }

            // Dispose scope
            scope?.Dispose();

            logger.LogInformation(
                "Completed execution lifecycle for task {TaskKey} instance {InstanceId}",
                instance.TaskKey,
                instance.InstanceId);
        }
    }

    /// <summary>
    /// Resolves the task instance from DI and executes it.
    /// </summary>
    private async Task ResolveAndExecuteTaskAsync(
        IServiceProvider scopedProvider,
        TaskDefinition definition,
        TaskInstance instance,
        CancellationToken cancellationToken)
    {
        object? taskInstance = null;

        try
        {
            // Resolve task instance from DI
            taskInstance = scopedProvider.GetRequiredService(definition.TaskClrType);

            logger.LogDebug(
                "Resolved task instance of type {TaskType} for {TaskKey}",
                definition.TaskClrType.Name,
                instance.TaskKey);
        }
        catch (InvalidOperationException ex)
        {
            // DI resolution failure
            logger.LogError(
                ex,
                "Failed to resolve task type {TaskType} from DI container. " +
                "Ensure the task type is registered or has a public constructor with resolvable dependencies.",
                definition.TaskClrType.Name);

            throw new InvalidOperationException(
                $"Dependency injection resolution failed for task type {definition.TaskClrType.Name}. " +
                $"Error: {ex.Message}",
                ex);
        }

        // Execute based on task type
        if (taskInstance is RecurringTask recurringTask)
        {
            // Execute recurring task (no parameters)
            logger.LogDebug(
                "Executing RecurringTask {TaskKey} instance {InstanceId}",
                instance.TaskKey,
                instance.InstanceId);

            await recurringTask.ExecuteAsync(cancellationToken);
        }
        else
        {
            // Check if it's a TriggeredTask<TParam>
            var taskType = definition.TaskClrType;
            var baseType = taskType.BaseType;

            while (baseType != null && !baseType.IsGenericType)
            {
                baseType = baseType.BaseType;
            }

            if (baseType != null &&
                baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition() == typeof(TriggeredTask<>))
            {
                // It's a TriggeredTask<TParam>
                var parameterType = baseType.GetGenericArguments()[0];

                logger.LogDebug(
                    "Executing TriggeredTask<{ParamType}> {TaskKey} instance {InstanceId}",
                    parameterType.Name,
                    instance.TaskKey,
                    instance.InstanceId);

                // Deserialize parameters
                object? parameters = null;
                if (!string.IsNullOrEmpty(instance.Parameters))
                {
                    try
                    {
                        parameters = JsonSerializer.Deserialize(instance.Parameters, parameterType);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(
                            ex,
                            "Failed to deserialize parameters for task {TaskKey} instance {InstanceId}. " +
                            "Parameters JSON: {Json}",
                            instance.TaskKey,
                            instance.InstanceId,
                            instance.Parameters);

                        throw new InvalidOperationException(
                            $"Failed to deserialize task parameters: {ex.Message}",
                            ex);
                    }
                }

                // Call ExecuteAsync via reflection
                var executeMethod = taskType.GetMethod("ExecuteAsync");
                if (executeMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Task type {taskType.Name} does not have ExecuteAsync method");
                }

                var executeTask = executeMethod.Invoke(taskInstance, new[] { parameters, cancellationToken });
                if (executeTask is Task task)
                {
                    await task;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"ExecuteAsync method did not return a Task for {taskType.Name}");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Task type {taskType.Name} does not inherit from RecurringTask or TriggeredTask<TParam>");
            }
        }

        logger.LogDebug(
            "Task execution method completed for {TaskKey} instance {InstanceId}",
            instance.TaskKey,
            instance.InstanceId);
    }

    /// <summary>
    /// Updates the task instance state in the metadata store.
    /// </summary>
    private async Task UpdateTaskStateAsync(
        TaskInstance instance,
        TaskState newState,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        instance.State = newState;
        instance.ErrorMessage = errorMessage;

        if (newState == TaskState.Succeeded ||
            newState == TaskState.Failed ||
            newState == TaskState.Terminated ||
            newState == TaskState.Cancelled)
        {
            instance.CompletedAt = DateTime.UtcNow;
        }

        await metadataStore.UpdateTaskStateAsync(
            instance.InstanceId,
            newState,
            errorMessage,
            cancellationToken);

        logger.LogDebug(
            "Updated task {TaskKey} instance {InstanceId} state to {State}",
            instance.TaskKey,
            instance.InstanceId,
            newState);
    }

    /// <summary>
    /// Handles retry logic for failed tasks.
    /// </summary>
    private async Task HandleRetryLogicAsync(
        TaskInstance instance,
        TaskDefinition definition,
        CancellationToken cancellationToken)
    {
        // Check if retries are configured and remaining
        if (definition.RetryCount <= 0)
        {
            logger.LogDebug(
                "No retries configured for task {TaskKey}. Marking as Terminated.",
                instance.TaskKey);

            await UpdateTaskStateAsync(
                instance,
                TaskState.Terminated,
                instance.ErrorMessage,
                cancellationToken);

            return;
        }

        if (instance.RetryAttempt >= definition.RetryCount)
        {
            logger.LogWarning(
                "Task {TaskKey} instance {InstanceId} exhausted all {RetryCount} retry attempts. Marking as Terminated.",
                instance.TaskKey,
                instance.InstanceId,
                definition.RetryCount);

            await UpdateTaskStateAsync(
                instance,
                TaskState.Terminated,
                instance.ErrorMessage,
                cancellationToken);

            return;
        }

        // Increment retry attempt
        instance.RetryAttempt++;

        logger.LogInformation(
            "Retrying task {TaskKey} instance {InstanceId}. Attempt {Attempt}/{Max}",
            instance.TaskKey,
            instance.InstanceId,
            instance.RetryAttempt,
            definition.RetryCount);

        // Update retry attempt in metadata store
        await metadataStore.SaveTaskInstanceAsync(instance, cancellationToken);

        // Re-publish task execution event for retry
        try
        {
            var retryEvent = new TaskExecutionEvent
            {
                InstanceId = instance.InstanceId,
                TaskKey = instance.TaskKey,
                Parameters = instance.Parameters,
                RequestedAt = DateTime.UtcNow
            };

            await eventBus.PublishAsync(retryEvent);

            logger.LogInformation(
                "Published retry event for task {TaskKey} instance {InstanceId}",
                instance.TaskKey,
                instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish retry event for task {TaskKey} instance {InstanceId}: {Message}",
                instance.TaskKey,
                instance.InstanceId,
                ex.Message);

            // Mark as terminated if we can't retry
            await UpdateTaskStateAsync(
                instance,
                TaskState.Terminated,
                $"Failed to publish retry event: {ex.Message}",
                cancellationToken);
        }
    }
}
