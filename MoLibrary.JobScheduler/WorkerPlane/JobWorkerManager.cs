using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Helpers;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Coordinates job execution across worker threads and manages event subscriptions.
/// Implements IHostedService to subscribe to job execution events and orchestrate
/// job execution with concurrency control and thread limiting.
/// </summary>
public class JobWorkerManager(
    IOptions<ModuleJobSchedulerOption> options,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    JobOrchestrator jobOrchestrator,
    IMoJobScheduleMetadataStore metadataStore,
    IReadOnlyList<JobDefinition> jobDefinitions,
    ILogger<JobWorkerManager> logger) : IHostedService
{
    private readonly ModuleJobSchedulerOption _options = options.Value;
    private readonly List<IAsyncDisposable> _eventSubscriptions = new();
    private readonly HashSet<string> _subscribedProjects = new();
    private SemaphoreSlim? _workerThreadSemaphore;

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

        // Extract unique FromProject values from registered job definitions
        var projectsToSubscribe = jobDefinitions
            .Where(d => !string.IsNullOrWhiteSpace(d.FromProject))
            .Select(d => d.FromProject)
            .Distinct()
            .ToList();

        if (projectsToSubscribe.Count == 0)
        {
            logger.LogWarning("No job definitions with valid FromProject found, no subscriptions created");
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Subscribing to JobExecutionEvent from {Count} project(s): {Projects}",
            projectsToSubscribe.Count,
            string.Join(", ", projectsToSubscribe));

        // Subscribe to each project's topic
        foreach (var fromProject in projectsToSubscribe)
        {
            var topicName = JobEventTopicHelper.GetTopicName<JobExecutionEvent>(fromProject);
            var subscription = eventBus.Subscribe<JobExecutionEvent>(HandleJobExecutionAsync, topicName);

            _eventSubscriptions.Add(subscription);
            _subscribedProjects.Add(fromProject);

            logger.LogInformation(
                "Subscribed to topic: {TopicName} (Project: {Project})",
                topicName,
                fromProject);
        }

        logger.LogInformation(
            "JobWorkerManager started with {SubscriptionCount} subscription(s)",
            _eventSubscriptions.Count);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the worker manager by unsubscribing from events and waiting for in-flight jobs.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobWorkerManager stopping...");

        // Unsubscribe from all events
        if (_eventSubscriptions.Count > 0)
        {
            logger.LogInformation("Unsubscribing from {Count} topic(s)", _eventSubscriptions.Count);

            foreach (var subscription in _eventSubscriptions)
            {
                await subscription.DisposeAsync();
            }

            _eventSubscriptions.Clear();
            _subscribedProjects.Clear();

            logger.LogInformation("All subscriptions disposed");
        }

        // TODO Print all in-flight jobs


        // Dispose semaphore
        _workerThreadSemaphore?.Dispose();
        _workerThreadSemaphore = null;

        logger.LogInformation("JobWorkerManager stopped");
    }

    /// <summary>
    /// Handles job execution events from the event bus.
    /// </summary>
    /// <param name="executionEvent">The job execution event.</param>
    private async Task HandleJobExecutionAsync(JobExecutionEvent executionEvent)
    {
        logger.LogDebug(
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
    private async Task ExecuteJobAsync(JobExecutionEvent executionEvent)
    {
        var workerSlotAcquired = false;
        var concurrencySlotAcquired = false;
        JobInstance? instance = null;

        try
        {
            // Acquire worker thread slot (if limit configured)
            if (_workerThreadSemaphore != null)
            {
                await _workerThreadSemaphore.WaitAsync();
                workerSlotAcquired = true;

                logger.LogDebug(
                    "Acquired worker thread slot for job {JobKey} instance {InstanceId}",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);
            }
            
            instance = await metadataStore.GetJobInstanceAsync(executionEvent.InstanceId);
            if (instance == null)
            {
                logger.LogError(
                    "Job instance not found: {InstanceId}",
                    executionEvent.InstanceId);
                return;
            }


            logger.LogDebug(
                "Starting execution for job {JobKey} instance {InstanceId}",
                executionEvent.JobKey,
                executionEvent.InstanceId);

            await jobOrchestrator.ExecuteAsync(instance, executionEvent);

            logger.LogDebug(
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
        }
        finally
        {
            if (workerSlotAcquired && _workerThreadSemaphore != null)
            {
                _workerThreadSemaphore.Release();
                logger.LogDebug(
                    "Released worker thread slot for job {JobKey} instance {InstanceId}",
                    executionEvent.JobKey,
                    executionEvent.InstanceId);
            }
        }
    }
}
