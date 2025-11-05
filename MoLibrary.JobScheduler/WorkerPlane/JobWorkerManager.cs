using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Coordinates job execution across worker threads and manages event subscriptions.
/// Implements IHostedService to subscribe to job execution events and orchestrate
/// job execution with concurrency control and thread limiting.
/// </summary>
/// <remarks>
/// <para>
/// JobWorkerManager is the entry point for job execution in the worker plane.
/// It subscribes to JobExecutionEvent from the event bus and coordinates execution by:
/// </para>
/// <list type="bullet">
/// <item><description>Enforcing worker thread limits via SemaphoreSlim (if configured)</description></item>
/// <item><description>Checking job-specific concurrency limits via ConcurrencyGuard</description></item>
/// <item><description>Delegating execution to JobExecutor</description></item>
/// <item><description>Marking jobs as Skipped when concurrency limits are exceeded</description></item>
/// <item><description>Tracking in-flight jobs for graceful shutdown</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b> This class handles concurrent event delivery and ensures
/// proper synchronization when tracking in-flight jobs and managing worker threads.
/// </para>
/// </remarks>
public class JobWorkerManager(
    IOptions<ModuleJobSchedulerOption> options,
    IMoEventBus eventBus,
    ConcurrencyGuard concurrencyGuard,
    JobExecutor jobExecutor,
    JobRegistry jobRegistry,
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobWorkerManager> logger) : IHostedService
{
    private readonly ModuleJobSchedulerOption _options = options.Value;
    private IDisposable? _eventSubscription;
    private SemaphoreSlim? _workerThreadSemaphore;
    private readonly SemaphoreSlim _shutdownSemaphore = new(1, 1);
    private int _inFlightJobCount = 0;
    private readonly object _inFlightLock = new();

    /// <summary>
    /// Starts the worker manager by subscribing to job execution events.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobWorkerManager starting...");

        // Initialize worker thread semaphore if MaxWorkerExecutionThreads is configured
        if (_options.MaxWorkerExecutionThreads is > 0)
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

        // Subscribe to JobExecutionEvent
        _eventSubscription = eventBus.Subscribe<MoJobExecutionEvent>(HandleJobExecutionAsync);

        logger.LogInformation("JobWorkerManager started and subscribed to JobExecutionEvent");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the worker manager by unsubscribing from events and waiting for in-flight jobs.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobWorkerManager stopping...");

        // Unsubscribe from events to prevent new jobs
        _eventSubscription?.Dispose();
        _eventSubscription = null;

        logger.LogInformation("Unsubscribed from JobExecutionEvent");

        // Wait for all in-flight jobs to complete
        await _shutdownSemaphore.WaitAsync(cancellationToken);
        try
        {
            var inFlightCount = GetInFlightJobCount();
            if (inFlightCount > 0)
            {
                logger.LogInformation(
                    "Waiting for {Count} in-flight job(s) to complete...",
                    inFlightCount);

                // Poll until all jobs complete or cancellation requested
                while (GetInFlightJobCount() > 0 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken);
                }

                inFlightCount = GetInFlightJobCount();
                if (inFlightCount > 0)
                {
                    logger.LogWarning(
                        "Shutdown cancelled with {Count} job(s) still in flight",
                        inFlightCount);
                }
                else
                {
                    logger.LogInformation("All in-flight jobs completed");
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

        logger.LogInformation("JobWorkerManager stopped");
    }

    /// <summary>
    /// Handles job execution events from the event bus.
    /// </summary>
    /// <param name="executionEvent">The job execution event.</param>
    private async Task HandleJobExecutionAsync(MoJobExecutionEvent executionEvent)
    {
        logger.LogInformation(
            "Received JobExecutionEvent for job {JobKey}, InstanceId: {InstanceId}",
            executionEvent.JobKey,
            executionEvent.InstanceId);

        // Don't block the event handler - execute asynchronously
        _ = Task.Run(async () =>
        {
            await ExecuteJobAsync(executionEvent);
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes a job with concurrency control and thread limiting.
    /// </summary>
    private async Task ExecuteJobAsync(MoJobExecutionEvent executionEvent)
    {
        var workerSlotAcquired = false;
        var concurrencySlotAcquired = false;
        JobDefinition? definition = null;
        JobInstance? instance = null;

        try
        {
            // Increment in-flight job counter
            IncrementInFlightJobCount();

            // Step 1: Acquire worker thread slot (if limit configured)
            if (_workerThreadSemaphore != null)
            {
                await _workerThreadSemaphore.WaitAsync();
                workerSlotAcquired = true;

                logger.LogDebug(
                    "Acquired worker thread slot for job {JobKey} instance {InstanceId}",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);
            }

            // Get job definition and instance
            definition = await jobRegistry.GetDefinitionAsync(executionEvent.JobKey);
            if (definition == null)
            {
                logger.LogError(
                    "Job definition not found for {JobKey}, instance {InstanceId}",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);
                return;
            }

            instance = await metadataStore.GetJobInstanceAsync(executionEvent.InstanceId);
            if (instance == null)
            {
                logger.LogError(
                    "Job instance not found: {InstanceId}",
                    executionEvent.InstanceId);
                return;
            }

            // Step 2: Try to acquire job concurrency slot
            concurrencySlotAcquired = await concurrencyGuard.TryAcquireAsync(
                executionEvent.JobKey,
                definition.MaxConcurrency);

            if (!concurrencySlotAcquired)
            {
                // Concurrency limit reached - mark as Skipped
                logger.LogWarning(
                    "Concurrency limit reached for job {JobKey}. Instance {InstanceId} will be skipped.",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);

                await metadataStore.UpdateJobStateAsync(
                    executionEvent.InstanceId,
                    JobState.Skipped,
                    $"Concurrency limit of {definition.MaxConcurrency} reached");

                return;
            }

            logger.LogInformation(
                "Starting execution for job {JobKey} instance {InstanceId}",
                executionEvent.JobKey,
                executionEvent.InstanceId);

            // Step 3: Execute job via JobExecutor
            await jobExecutor.ExecuteAsync(instance, definition);

            logger.LogInformation(
                "Completed execution for job {JobKey} instance {InstanceId}",
                executionEvent.JobKey,
                executionEvent.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error handling job execution event for {JobKey} instance {InstanceId}: {Message}",
                executionEvent.JobKey,
                executionEvent.InstanceId,
                ex.Message);

            // Try to mark instance as failed
            try
            {
                if (instance != null)
                {
                    await metadataStore.UpdateJobStateAsync(
                        executionEvent.InstanceId,
                        JobState.Failed,
                        $"Worker error: {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update job state after worker error: {Message}",
                    updateEx.Message);
            }
        }
        finally
        {
            // Step 5: Always release resources
            if (concurrencySlotAcquired)
            {
                await concurrencyGuard.ReleaseAsync(executionEvent.JobKey);
                logger.LogDebug(
                    "Released concurrency slot for job {JobKey}",
                    executionEvent.JobKey);
            }

            if (workerSlotAcquired && _workerThreadSemaphore != null)
            {
                _workerThreadSemaphore.Release();
                logger.LogDebug(
                    "Released worker thread slot for job {JobKey} instance {InstanceId}",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);
            }

            // Decrement in-flight job counter
            DecrementInFlightJobCount();
        }
    }

    /// <summary>
    /// Increments the in-flight job counter.
    /// </summary>
    private void IncrementInFlightJobCount()
    {
        lock (_inFlightLock)
        {
            _inFlightJobCount++;
        }
    }

    /// <summary>
    /// Decrements the in-flight job counter.
    /// </summary>
    private void DecrementInFlightJobCount()
    {
        lock (_inFlightLock)
        {
            _inFlightJobCount--;
            if (_inFlightJobCount < 0)
            {
                logger.LogWarning("In-flight job count went negative, resetting to 0");
                _inFlightJobCount = 0;
            }
        }
    }

    /// <summary>
    /// Gets the current in-flight job count.
    /// </summary>
    private int GetInFlightJobCount()
    {
        lock (_inFlightLock)
        {
            return _inFlightJobCount;
        }
    }
}
