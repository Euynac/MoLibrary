using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.ControlPlane;
using MoLibrary.TaskScheduler.Events;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.WorkerPlane;

/// <summary>
/// Coordinates task execution across worker threads and manages event subscriptions.
/// Implements IHostedService to subscribe to task execution events and orchestrate
/// task execution with concurrency control and thread limiting.
/// </summary>
/// <remarks>
/// <para>
/// TaskWorkerManager is the entry point for task execution in the worker plane.
/// It subscribes to TaskExecutionEvent from the event bus and coordinates execution by:
/// </para>
/// <list type="bullet">
/// <item><description>Enforcing worker thread limits via SemaphoreSlim (if configured)</description></item>
/// <item><description>Checking task-specific concurrency limits via ConcurrencyGuard</description></item>
/// <item><description>Delegating execution to TaskExecutor</description></item>
/// <item><description>Marking tasks as Skipped when concurrency limits are exceeded</description></item>
/// <item><description>Tracking in-flight tasks for graceful shutdown</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b> This class handles concurrent event delivery and ensures
/// proper synchronization when tracking in-flight tasks and managing worker threads.
/// </para>
/// </remarks>
public class TaskWorkerManager(
    IOptions<ModuleTaskSchedulerOption> options,
    IMoEventBus eventBus,
    ConcurrencyGuard concurrencyGuard,
    TaskExecutor taskExecutor,
    TaskRegistry taskRegistry,
    IMoTaskScheduleMetadataStore metadataStore,
    ILogger<TaskWorkerManager> logger) : IHostedService
{
    private readonly ModuleTaskSchedulerOption _options = options.Value;
    private IDisposable? _eventSubscription;
    private SemaphoreSlim? _workerThreadSemaphore;
    private readonly SemaphoreSlim _shutdownSemaphore = new(1, 1);
    private int _inFlightTaskCount = 0;
    private readonly object _inFlightLock = new();

    /// <summary>
    /// Starts the worker manager by subscribing to task execution events.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TaskWorkerManager starting...");

        // Initialize worker thread semaphore if MaxWorkerExecutionThreads is configured
        if (_options.MaxWorkerExecutionThreads.HasValue && _options.MaxWorkerExecutionThreads.Value > 0)
        {
            _workerThreadSemaphore = new SemaphoreSlim(
                _options.MaxWorkerExecutionThreads.Value,
                _options.MaxWorkerExecutionThreads.Value);

            logger.LogInformation(
                "Worker thread limit configured: {MaxThreads} concurrent executions",
                _options.MaxWorkerExecutionThreads.Value);
        }
        else
        {
            logger.LogInformation("Worker thread limit: unlimited");
        }

        // Subscribe to TaskExecutionEvent
        _eventSubscription = eventBus.Subscribe<TaskExecutionEvent>(HandleTaskExecutionAsync);

        logger.LogInformation("TaskWorkerManager started and subscribed to TaskExecutionEvent");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the worker manager by unsubscribing from events and waiting for in-flight tasks.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TaskWorkerManager stopping...");

        // Unsubscribe from events to prevent new tasks
        _eventSubscription?.Dispose();
        _eventSubscription = null;

        logger.LogInformation("Unsubscribed from TaskExecutionEvent");

        // Wait for all in-flight tasks to complete
        await _shutdownSemaphore.WaitAsync(cancellationToken);
        try
        {
            var inFlightCount = GetInFlightTaskCount();
            if (inFlightCount > 0)
            {
                logger.LogInformation(
                    "Waiting for {Count} in-flight task(s) to complete...",
                    inFlightCount);

                // Poll until all tasks complete or cancellation requested
                while (GetInFlightTaskCount() > 0 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken);
                }

                inFlightCount = GetInFlightTaskCount();
                if (inFlightCount > 0)
                {
                    logger.LogWarning(
                        "Shutdown cancelled with {Count} task(s) still in flight",
                        inFlightCount);
                }
                else
                {
                    logger.LogInformation("All in-flight tasks completed");
                }
            }
        }
        finally
        {
            _shutdownSemaphore.Release();
        }

        // Dispose semaphore
        _workerThreadSemaphore?.Dispose();
        _workerThreadSemaphore = null;

        logger.LogInformation("TaskWorkerManager stopped");
    }

    /// <summary>
    /// Handles task execution events from the event bus.
    /// </summary>
    /// <param name="executionEvent">The task execution event.</param>
    private async Task HandleTaskExecutionAsync(TaskExecutionEvent executionEvent)
    {
        if (executionEvent == null)
        {
            logger.LogWarning("Received null TaskExecutionEvent, ignoring");
            return;
        }

        logger.LogInformation(
            "Received TaskExecutionEvent for task {TaskKey}, InstanceId: {InstanceId}",
            executionEvent.TaskKey,
            executionEvent.InstanceId);

        // Don't block the event handler - execute asynchronously
        _ = Task.Run(async () =>
        {
            await ExecuteTaskAsync(executionEvent);
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes a task with concurrency control and thread limiting.
    /// </summary>
    private async Task ExecuteTaskAsync(TaskExecutionEvent executionEvent)
    {
        bool workerSlotAcquired = false;
        bool concurrencySlotAcquired = false;
        TaskDefinition? definition = null;
        TaskInstance? instance = null;

        try
        {
            // Increment in-flight task counter
            IncrementInFlightTaskCount();

            // Step 1: Acquire worker thread slot (if limit configured)
            if (_workerThreadSemaphore != null)
            {
                await _workerThreadSemaphore.WaitAsync();
                workerSlotAcquired = true;

                logger.LogDebug(
                    "Acquired worker thread slot for task {TaskKey} instance {InstanceId}",
                    executionEvent.TaskKey,
                    executionEvent.InstanceId);
            }

            // Get task definition and instance
            definition = await taskRegistry.GetDefinitionAsync(executionEvent.TaskKey);
            if (definition == null)
            {
                logger.LogError(
                    "Task definition not found for {TaskKey}, instance {InstanceId}",
                    executionEvent.TaskKey,
                    executionEvent.InstanceId);
                return;
            }

            instance = await metadataStore.GetTaskInstanceAsync(executionEvent.InstanceId);
            if (instance == null)
            {
                logger.LogError(
                    "Task instance not found: {InstanceId}",
                    executionEvent.InstanceId);
                return;
            }

            // Step 2: Try to acquire task concurrency slot
            concurrencySlotAcquired = await concurrencyGuard.TryAcquireAsync(
                executionEvent.TaskKey,
                definition.MaxConcurrency);

            if (!concurrencySlotAcquired)
            {
                // Concurrency limit reached - mark as Skipped
                logger.LogWarning(
                    "Concurrency limit reached for task {TaskKey}. Instance {InstanceId} will be skipped.",
                    executionEvent.TaskKey,
                    executionEvent.InstanceId);

                await metadataStore.UpdateTaskStateAsync(
                    executionEvent.InstanceId,
                    TaskState.Skipped,
                    $"Concurrency limit of {definition.MaxConcurrency} reached");

                return;
            }

            logger.LogInformation(
                "Starting execution for task {TaskKey} instance {InstanceId}",
                executionEvent.TaskKey,
                executionEvent.InstanceId);

            // Step 3: Execute task via TaskExecutor
            await taskExecutor.ExecuteAsync(instance, definition);

            logger.LogInformation(
                "Completed execution for task {TaskKey} instance {InstanceId}",
                executionEvent.TaskKey,
                executionEvent.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error handling task execution event for {TaskKey} instance {InstanceId}: {Message}",
                executionEvent.TaskKey,
                executionEvent.InstanceId,
                ex.Message);

            // Try to mark instance as failed
            try
            {
                if (instance != null)
                {
                    await metadataStore.UpdateTaskStateAsync(
                        executionEvent.InstanceId,
                        TaskState.Failed,
                        $"Worker error: {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update task state after worker error: {Message}",
                    updateEx.Message);
            }
        }
        finally
        {
            // Step 5: Always release resources
            if (concurrencySlotAcquired)
            {
                await concurrencyGuard.ReleaseAsync(executionEvent.TaskKey);
                logger.LogDebug(
                    "Released concurrency slot for task {TaskKey}",
                    executionEvent.TaskKey);
            }

            if (workerSlotAcquired && _workerThreadSemaphore != null)
            {
                _workerThreadSemaphore.Release();
                logger.LogDebug(
                    "Released worker thread slot for task {TaskKey} instance {InstanceId}",
                    executionEvent.TaskKey,
                    executionEvent.InstanceId);
            }

            // Decrement in-flight task counter
            DecrementInFlightTaskCount();
        }
    }

    /// <summary>
    /// Increments the in-flight task counter.
    /// </summary>
    private void IncrementInFlightTaskCount()
    {
        lock (_inFlightLock)
        {
            _inFlightTaskCount++;
        }
    }

    /// <summary>
    /// Decrements the in-flight task counter.
    /// </summary>
    private void DecrementInFlightTaskCount()
    {
        lock (_inFlightLock)
        {
            _inFlightTaskCount--;
            if (_inFlightTaskCount < 0)
            {
                logger.LogWarning("In-flight task count went negative, resetting to 0");
                _inFlightTaskCount = 0;
            }
        }
    }

    /// <summary>
    /// Gets the current in-flight task count.
    /// </summary>
    private int GetInFlightTaskCount()
    {
        lock (_inFlightLock)
        {
            return _inFlightTaskCount;
        }
    }
}
