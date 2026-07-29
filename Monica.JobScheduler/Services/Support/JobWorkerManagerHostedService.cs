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
    IReadOnlyList<JobDefinition> jobDefinitions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobWorkerManagerHostedService> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, serviceScopeFactory, logger)
{
    private readonly ModuleJobSchedulerOption _options = jobSchedulerOptions.Value;
    private readonly List<IAsyncDisposable> _eventSubscriptions = [];
    private readonly HashSet<string> _subscribedProjects = [];
    private readonly JobExecutionRegistry _executionRegistry = new();
    private SemaphoreSlim? _workerThreadSemaphore;
    private CancellationToken _workerStoppingToken;

    public override string ServiceName => "JobWorkerManager";
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.JobScheduler);

    /// <summary>
    /// Unsubscribes from events and releases worker resources before the background operation stops.
    /// </summary>
    protected override async Task OnStoppingAsync(CancellationToken cancellationToken)
    {
        _executionRegistry.CloseAdmission();

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

        if (_executionRegistry.Count > 0)
        {
            RecordState(
                $"Waiting for {_executionRegistry.Count} in-flight job execution(s) to acknowledge host cancellation",
                logLevel: LogLevel.Information);
            try
            {
                await _executionRegistry.DrainAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RecordState(
                    $"Host shutdown stopped waiting for {_executionRegistry.Count} in-flight job execution(s)",
                    HostedServiceState.Degraded,
                    logLevel: LogLevel.Warning);
            }
        }

        try
        {
            await jobOrchestrator.DrainLateExecutionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RecordState(
                "Host shutdown stopped waiting for cancellation-ignoring jobs to release their execution scopes",
                HostedServiceState.Degraded,
                logLevel: LogLevel.Warning);
        }

        if (_executionRegistry.Count == 0)
        {
            // Executions release their slots before removal, so the semaphore can now be disposed safely.
            _workerThreadSemaphore?.Dispose();
            _workerThreadSemaphore = null;
        }
        else
        {
            RecordState(
                "The worker semaphore remains allocated because job executions outlived the shutdown wait boundary",
                HostedServiceState.Degraded,
                logLevel: LogLevel.Warning);
        }

        await base.OnStoppingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the worker manager by subscribing to events and starting background tasks.
    /// </summary>
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        _workerStoppingToken = stoppingToken;
        _executionRegistry.OpenAdmission();

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
            var subscription = await eventBus.SubscribeAsync<JobExecutionEvent>(
                HandleJobExecutionAsync,
                topicName);

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
    /// <param name="cancellationToken">Signals that the event delivery is no longer waiting.</param>
    private Task HandleJobExecutionAsync(
        JobExecutionEvent executionEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(executionEvent.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            RecordState(
                $"Ignored JobExecutionEvent for foreign scope {executionEvent.SchedulerScopeKey}",
                logLevel: LogLevel.Debug);
            return Task.CompletedTask;
        }

        RecordState(
            $"Received JobExecutionEvent for job {executionEvent.JobKey}, InstanceId: {executionEvent.InstanceId}",
            logLevel: LogLevel.Debug);

        // Acknowledged job execution intentionally outlives this message-delivery token, but remains owned by the
        // worker host so application shutdown can cancel and await the orchestration lifecycle.
        if (!_executionRegistry.TryStart(
                executionEvent.InstanceId,
                () => ExecuteJobAsync(executionEvent, _workerStoppingToken),
                out var executionTask))
        {
            RecordState(
                $"Ignored duplicate or shutdown-time execution delivery for job instance {executionEvent.InstanceId}",
                logLevel: LogLevel.Warning);
            return Task.CompletedTask;
        }

        _ = ObserveExecutionAsync(executionEvent.InstanceId, executionTask);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes a job with concurrency control and thread limiting.
    /// </summary>
    private async Task ExecuteJobAsync(
        JobExecutionEvent executionEvent,
        CancellationToken stoppingToken)
    {
        var workerSlotAcquired = false;

        try
        {
            // Acquire worker thread slot (if limit configured)
            if (_workerThreadSemaphore != null)
            {
                await _workerThreadSemaphore.WaitAsync(stoppingToken);
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

            await jobOrchestrator.ExecuteAsync(instance, executionEvent, stoppingToken);

            RecordState(
                $"Completed execution for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                logLevel: LogLevel.Debug);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            RecordState(
                $"Host cancellation stopped job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                logLevel: LogLevel.Information);
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
                await jobOrchestrator.WaitForExecutionReleaseAsync(executionEvent.InstanceId);
                _workerThreadSemaphore.Release();
                RecordState(
                    $"Released worker thread slot for job {executionEvent.JobKey} instance {executionEvent.InstanceId}",
                    logLevel: LogLevel.Debug);
            }
        }
    }

    private async Task ObserveExecutionAsync(string instanceId, Task executionTask)
    {
        try
        {
            await executionTask;
        }
        catch (Exception exception)
        {
            // ExecuteJobAsync normally records terminal failures itself. This guard ensures unexpected observer faults
            // are never left unobserved while the event delivery has already been acknowledged.
            RecordState(
                $"Unexpected observer failure for job instance {instanceId}: {exception.Message}",
                HostedServiceState.Degraded,
                exception,
                LogLevel.Error);
        }
    }
}
