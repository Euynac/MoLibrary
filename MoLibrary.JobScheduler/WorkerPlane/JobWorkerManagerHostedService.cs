using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Helpers;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Coordinates job execution across worker threads and manages event subscriptions.
/// Inherits from MoBackgroundService for enhanced observability with state tracking.
/// </summary>
public class JobWorkerManagerHostedService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleJobSchedulerOption> jobSchedulerOptions,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    JobOrchestrator jobOrchestrator,
    IMoJobMetadataRepository metadataRepository,
    IReadOnlyList<JobDefinition> jobDefinitions,
    ILogger<JobWorkerManagerHostedService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    private readonly ModuleJobSchedulerOption _options = jobSchedulerOptions.Value;
    private readonly List<IAsyncDisposable> _eventSubscriptions = [];
    private readonly HashSet<string> _subscribedProjects = [];
    private SemaphoreSlim? _workerThreadSemaphore;

    public override string ServiceName => "JobWorkerManager";

    /// <summary>
    /// Stops the worker manager by unsubscribing from events and waiting for in-flight jobs.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from all events
        if (_eventSubscriptions.Count > 0)
        {
            RecordState($"Unsubscribing from {_eventSubscriptions.Count} topic(s)", givenLogLevel: LogLevel.Information);

            foreach (var subscription in _eventSubscriptions)
            {
                await subscription.DisposeAsync();
            }

            _eventSubscriptions.Clear();
            _subscribedProjects.Clear();

            RecordState("All subscriptions disposed", givenLogLevel: LogLevel.Information);
        }

        // TODO Print all in-flight jobs

        // Dispose semaphore
        _workerThreadSemaphore?.Dispose();
        _workerThreadSemaphore = null;

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Starts the worker manager by subscribing to events and starting background tasks.
    /// </summary>
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        // Initialize worker thread semaphore if MaxWorkerExecutionThreads is configured
        if (_options.MaxWorkerExecutionThreads is > 0)
        {
            _workerThreadSemaphore = new SemaphoreSlim(
                _options.MaxWorkerExecutionThreads.Value,
                _options.MaxWorkerExecutionThreads.Value);

            RecordState(
                $"Worker thread limit configured: {_options.MaxWorkerExecutionThreads.Value} concurrent executions",
                givenLogLevel: LogLevel.Information);
        }
        else
        {
            RecordState("Worker thread limit: unlimited", givenLogLevel: LogLevel.Information);
        }

        // Extract unique FromProject values from registered job definitions
        var projectsToSubscribe = jobDefinitions
            .Where(d => !string.IsNullOrWhiteSpace(d.FromProject))
            .Select(d => d.FromProject)
            .Distinct()
            .ToList();

        if (projectsToSubscribe.Count == 0)
        {
            RecordState(
                "No job definitions with valid FromProject found, no subscriptions created",
                HostedServiceState.Degraded,
                givenLogLevel: LogLevel.Warning);
            return;
        }

        RecordState(
            $"Subscribing to JobExecutionEvent from {projectsToSubscribe.Count} project(s): {string.Join(", ", projectsToSubscribe)}",
            givenLogLevel: LogLevel.Information);

        // Subscribe to each project's topic
        foreach (var fromProject in projectsToSubscribe)
        {
            var topicName = JobEventTopicHelper.GetTopicName<JobExecutionEvent>(fromProject);
            var subscription = await eventBus.SubscribeAsync<JobExecutionEvent>(HandleJobExecutionAsync, topicName);

            _eventSubscriptions.Add(subscription);
            _subscribedProjects.Add(fromProject);

            RecordState(
                $"Subscribed to topic: {topicName} (Project: {fromProject})",
                givenLogLevel: LogLevel.Debug);
        }

        RecordState($"JobWorkerManager started with {_eventSubscriptions.Count} subscription(s)", givenLogLevel: LogLevel.Information);
    }

    /// <summary>
    /// Handles job execution events from the event bus.
    /// </summary>
    /// <param name="executionEvent">The job execution event.</param>
    private async Task HandleJobExecutionAsync(JobExecutionEvent executionEvent)
    {
        RecordState(
            $"Received JobExecutionEvent for job {executionEvent.JobKey}, InstanceId: {executionEvent.InstanceId}",
            givenLogLevel: LogLevel.Debug);

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

        try
        {
            // Acquire worker thread slot (if limit configured)
            if (_workerThreadSemaphore != null)
            {
                await _workerThreadSemaphore.WaitAsync();
                workerSlotAcquired = true;

                RecordState(
                    $"Acquired worker thread slot for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                    givenLogLevel: LogLevel.Debug);
            }

            var instance = await metadataRepository.GetInstanceAsync(executionEvent.InstanceId);
            if (instance == null)
            {
                RecordState(
                    $"Job instance not found: {executionEvent.InstanceId}",
                    givenLogLevel: LogLevel.Error);
                return;
            }

            if (instance.State != JobState.Enqueued)
            {
                RecordState(
                    $"Job instance is not in Enqueued state: {executionEvent.InstanceId}, skipped",
                    givenLogLevel: LogLevel.Error);
                return;
            }

            RecordState(
                $"Starting execution for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                givenLogLevel: LogLevel.Debug);

            await jobOrchestrator.ExecuteAsync(instance, executionEvent);

            RecordState(
                $"Completed execution for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                givenLogLevel: LogLevel.Debug);
        }
        catch (Exception ex)
        {
            RecordState(
                $"Error handling job execution event for {executionEvent.JobKey} instance {executionEvent.InstanceId}: {ex.Message}",
                exception: ex,
                givenLogLevel: LogLevel.Error);
        }
        finally
        {
            if (workerSlotAcquired && _workerThreadSemaphore != null)
            {
                _workerThreadSemaphore.Release();
                RecordState(
                    $"Released worker thread slot for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                    givenLogLevel: LogLevel.Debug);
            }
        }
    }
}
