using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Coordinates job execution across worker threads and manages event subscriptions.
/// Inherits from MoBackgroundService for enhanced observability with state tracking.
/// </summary>
public class JobWorkerManagerHostedService(
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IOptions<ModuleJobSchedulerOption> jobSchedulerOptions,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    JobOrchestrator jobOrchestrator,
    IJobMetadataRepository metadataRepository,
    IReadOnlyList<JobDefinition> jobDefinitions)
    : MoBackgroundService(observableManager, hostedServiceOptions)
{
    private readonly ModuleJobSchedulerOption _options = jobSchedulerOptions.Value;
    private readonly List<IAsyncDisposable> _eventSubscriptions = [];
    private readonly HashSet<string> _subscribedProjects = [];
    private SemaphoreSlim? _workerThreadSemaphore;

    public override string ServiceName => "JobWorkerManager";
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.JobScheduler);

    /// <summary>
    /// Stops the worker manager by unsubscribing from events and waiting for in-flight jobs.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from all events
        if (_eventSubscriptions.Count > 0)
        {
            RecordState($"Unsubscribing from {_eventSubscriptions.Count} topic(s)", logLevel: LogLevel.Information);

            foreach (var subscription in _eventSubscriptions)
            {
                await subscription.DisposeAsync();
            }

            _eventSubscriptions.Clear();
            _subscribedProjects.Clear();

            RecordState("All subscriptions disposed", logLevel: LogLevel.Information);
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
                logLevel: LogLevel.Information);
        }
        else
        {
            RecordState("Worker thread limit: unlimited", logLevel: LogLevel.Information);
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
                logLevel: LogLevel.Warning);
            return;
        }

        RecordState(
            $"Subscribing to JobExecutionEvent from {projectsToSubscribe.Count} project(s): {string.Join(", ", projectsToSubscribe)}",
            logLevel: LogLevel.Information);

        // Subscribe to each project's topic
        foreach (var fromProject in projectsToSubscribe)
        {
            var topicName = JobEventTopicHelper.GetProjectTopicName<JobExecutionEvent>(
                _options.SchedulerScopeKey,
                fromProject);
            var subscription = await eventBus.SubscribeAsync<JobExecutionEvent>(HandleJobExecutionAsync, topicName);

            _eventSubscriptions.Add(subscription);
            _subscribedProjects.Add(fromProject);

            RecordState(
                $"Subscribed to topic: {topicName} (Project: {fromProject})",
                logLevel: LogLevel.Debug);
        }

        RecordState($"JobWorkerManager started with {_eventSubscriptions.Count} subscription(s)", logLevel: LogLevel.Information);
    }

    /// <summary>
    /// Handles job execution events from the event bus.
    /// </summary>
    /// <param name="executionEvent">The job execution event.</param>
    private async Task HandleJobExecutionAsync(JobExecutionEvent executionEvent)
    {
        if (!string.Equals(executionEvent.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            RecordState(
                $"Ignored JobExecutionEvent for foreign scope {executionEvent.SchedulerScopeKey}",
                logLevel: LogLevel.Debug);
            return;
        }

        RecordState(
            $"Received JobExecutionEvent for job {executionEvent.JobKey}, InstanceId: {executionEvent.InstanceId}",
            logLevel: LogLevel.Debug);

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
                    logLevel: LogLevel.Debug);
            }

            var instance = await metadataRepository.GetInstanceAsync(executionEvent.InstanceId);
            if (instance == null)
            {
                RecordState(
                    $"Job instance not found: {executionEvent.InstanceId}",
                    logLevel: LogLevel.Error);
                return;
            }

            if (instance.State != JobState.Enqueued)
            {
                RecordState(
                    $"Job instance is not in Enqueued state: {executionEvent.InstanceId}, skipped",
                    logLevel: LogLevel.Error);
                return;
            }

            RecordState(
                $"Starting execution for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                logLevel: LogLevel.Debug);

            await jobOrchestrator.ExecuteAsync(instance, executionEvent);

            RecordState(
                $"Completed execution for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                logLevel: LogLevel.Debug);
        }
        catch (Exception ex)
        {
            RecordState(
                $"Error handling job execution event for {executionEvent.JobKey} instance {executionEvent.InstanceId}: {ex.Message}",
                exception: ex,
                logLevel: LogLevel.Error);
        }
        finally
        {
            if (workerSlotAcquired && _workerThreadSemaphore != null)
            {
                _workerThreadSemaphore.Release();
                RecordState(
                    $"Released worker thread slot for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                    logLevel: LogLevel.Debug);
            }
        }
    }
}
