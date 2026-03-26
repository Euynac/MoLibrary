using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Helpers;
using Monica.JobScheduler.Models;
using Monica.Modules;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Manages triggered and delayed job scheduling.
/// Handles job triggering events, delayed execution, cancellation, and recovery on restart.
/// </summary>
public class TriggeredJobScheduler(
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    IMoJobMetadataRepository metadataRepository,
    DelayedJobRecoveryService recoveryService,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<TriggeredJobScheduler> logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    // Delayed triggered job scheduling state
    private readonly ConcurrentDictionary<string, DelayedJobSchedule> _inFlightDelayedSchedules = new();

    // Event subscriptions
    private IAsyncDisposable? _triggeredJobSubscription;
    private IAsyncDisposable? _cancellationSubscription;
    private IAsyncDisposable? _manualExecutionSubscription;

    /// <summary>
    /// Initializes the triggered job scheduler.
    /// Subscribes to events and recovers scheduled jobs.
    /// </summary>
    /// <param name="eventBus">Event bus for job events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeAsync(IMoEventBus eventBus, CancellationToken cancellationToken = default)
    {
        // Subscribe to job triggered event
        _triggeredJobSubscription = await eventBus.SubscribeAsync<JobTriggeredEvent>(
            OnJobTriggeredAsync,
            JobEventTopicHelper.GetTopicName<JobTriggeredEvent>(_options.SchedulerScopeKey));
        logger.LogDebug("Subscribed to JobTriggeredEvent");

        // Subscribe to job cancellation event
        _cancellationSubscription = await eventBus.SubscribeAsync<JobCancellationRequestedEvent>(
            OnJobCancellationRequestedAsync,
            JobEventTopicHelper.GetTopicName<JobCancellationRequestedEvent>(_options.SchedulerScopeKey));
        logger.LogDebug("Subscribed to JobCancellationRequestedEvent");

        // Subscribe to manual job execution requests from Worker nodes
        _manualExecutionSubscription = await eventBus.SubscribeAsync<ManualJobExecutionRequestEvent>(
            OnManualJobExecutionRequestAsync,
            JobEventTopicHelper.GetTopicName<ManualJobExecutionRequestEvent>(_options.SchedulerScopeKey));
        logger.LogDebug("Subscribed to ManualJobExecutionRequestEvent");

        if (!_options.TriggeredJobDebugMode)
        {
            // Recover and reschedule delayed jobs
            await recoveryService.RecoverScheduledJobsAsync(
                (instance, definition, scheduledTime) =>
                {
                    ScheduleDelayedJob(instance, definition, scheduledTime);
                    return Task.CompletedTask;
                },
                cancellationToken);
        }
        else
        {
            logger.LogWarning("TriggeredJobDebugMode is enabled. Jobs will not be automatically scheduled.");
        }
    }

    /// <summary>
    /// Handles JobTriggeredEvent to create and schedule triggered jobs.
    /// </summary>
    private async Task OnJobTriggeredAsync(JobTriggeredEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            logger.LogDebug("Ignored JobTriggeredEvent for foreign scope {Scope}", evt.SchedulerScopeKey);
            return;
        }

        try
        {
            var definition = await cacheService.GetDefinitionAsync(evt.JobKey);
            if (definition == null || definition.IsDisabled)
            {
                logger.LogWarning("Triggered job {JobKey} not found or disabled", evt.JobKey);
                return;
            }

            var initialState = evt.Delay.HasValue ? JobState.Scheduled : JobState.Enqueued;
            var scheduledTime = evt.Delay.HasValue
                ? evt.TriggeredAt.Add(evt.Delay.Value)
                : (DateTime?)null;

            // Create instance with pre-generated ID
            var instance = await jobInstanceManager.CreateInstanceAsync(
                definition,
                parameters: evt.JobArgs,
                initialState,
                instanceId: evt.InstanceId,
                initDescription: evt.Delay.HasValue
                    ? $"JobTriggeredEvent delayed execution (delay: {evt.Delay.Value}, scheduled: {scheduledTime!.Value:yyyy-MM-dd HH:mm:ss})"
                    : "JobTriggeredEvent immediate execution");

            if (scheduledTime.HasValue)
            {
                instance.ScheduledExecutionTime = scheduledTime.Value;
                await metadataRepository.SaveInstanceAsync(instance);
            }

            logger.LogInformation(
                "Created job instance: {JobKey}, {InstanceId}, State: {State}, Scheduled: {Time}",
                evt.JobKey,
                instance.InstanceId,
                initialState,
                scheduledTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Immediate");

            if (scheduledTime is not null)
            {
                ScheduleDelayedJob(instance, definition, scheduledTime.Value);
            }
            else
            {
                // Pass JSON string directly (already serialized)
                await jobDispatcher.PublishJobExecutionEventAsync(instance, definition, evt.JobArgs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling JobTriggeredEvent for {JobKey}", evt.JobKey);
        }
    }

    /// <summary>
    /// Handles ManualJobExecutionRequestEvent from worker nodes.
    /// Loads the pre-created job instance and dispatches it for execution on the registry node.
    /// </summary>
    private async Task OnManualJobExecutionRequestAsync(ManualJobExecutionRequestEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            logger.LogDebug("Ignored ManualJobExecutionRequestEvent for foreign scope {Scope}", evt.SchedulerScopeKey);
            return;
        }

        try
        {
            var definition = await cacheService.GetDefinitionAsync(evt.JobKey);
            if (definition == null || definition.IsDisabled)
            {
                logger.LogWarning("Manual execution request for job {JobKey}: not found or disabled", evt.JobKey);
                return;
            }

            var instance = await metadataRepository.GetInstanceAsync(evt.InstanceId);
            if (instance == null)
            {
                logger.LogWarning(
                    "Manual execution request ignored because instance {InstanceId} was not found",
                    evt.InstanceId);
                return;
            }

            // Dispatch for execution
            await jobDispatcher.PublishJobExecutionEventAsync(instance, definition, evt.JobArgsJson);

            logger.LogInformation(
                "Registry processed manual execution request: {JobKey}, InstanceId: {InstanceId}",
                evt.JobKey, instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling ManualJobExecutionRequestEvent for {JobKey}", evt.JobKey);
        }
    }

    /// <summary>
    /// Schedules a delayed job using a one-shot timer.
    /// </summary>
    internal void ScheduleDelayedJob(JobInstance instance, JobDefinition definition, DateTime scheduledTime)
    {
        try
        {
            var now = DateTime.UtcNow;
            var dueTime = scheduledTime - now;

            // Execute immediately if scheduled time is in past
            if (dueTime < TimeSpan.Zero)
            {
                logger.LogWarning(
                    "Scheduled time in past for {InstanceId}, executing immediately",
                    instance.InstanceId);
                dueTime = TimeSpan.Zero;
            }

            // Check if delay exceeds Timer safety threshold
            var timerThreshold = TimeSpan.FromDays(_options.TimerSafetyThresholdDays);
            if (dueTime > timerThreshold)
            {
                logger.LogInformation(
                    "Triggered job instance {InstanceId} (JobKey: {JobKey}) delay ({Days} days) exceeds timer threshold ({ThresholdDays} days). Will be handled by long-interval scheduler. Scheduled time: {ScheduledTime}",
                    instance.InstanceId,
                    definition.JobKey,
                    dueTime.TotalDays,
                    _options.TimerSafetyThresholdDays,
                    scheduledTime);

                // Don't create Timer, will be handled by LongIntervalSchedulerService
                // The JobInstance is already in Scheduled state and persisted to database
                return;
            }

            var timer = new Timer(
                _ => OnDelayedJobTimerCallback(instance.InstanceId),
                null,
                dueTime,
                Timeout.InfiniteTimeSpan);

            var schedule = new DelayedJobSchedule
            {
                InstanceId = instance.InstanceId,
                JobKey = definition.JobKey,
                Timer = timer,
                ScheduledExecutionTime = scheduledTime
            };

            // Replace if already exists (shouldn't happen, but defensive)
            if (_inFlightDelayedSchedules.TryRemove(instance.InstanceId, out var oldSchedule))
            {
                oldSchedule.Timer?.Dispose();
                logger.LogWarning("Replaced existing delayed schedule for {InstanceId}", instance.InstanceId);
            }

            _inFlightDelayedSchedules[instance.InstanceId] = schedule;

            logger.LogDebug(
                "Scheduled delayed job {JobKey}, {InstanceId}, execution at {Time} (in {DueTime})",
                definition.JobKey,
                instance.InstanceId,
                scheduledTime,
                dueTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule delayed job {InstanceId}", instance.InstanceId);
        }
    }

    /// <summary>
    /// Timer callback for delayed job execution.
    /// </summary>
    private async void OnDelayedJobTimerCallback(string instanceId)
    {
        try
        {
            // Remove and dispose timer
            _inFlightDelayedSchedules.TryRemove(instanceId, out var schedule);
            schedule?.Timer?.Dispose();

            var instance = await metadataRepository.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                logger.LogWarning("Instance {InstanceId} not found in timer callback", instanceId);
                return;
            }

            // Validate state (handle cancellation)
            if (instance.State != JobState.Scheduled)
            {
                logger.LogWarning(
                    "Instance {InstanceId} not in Scheduled state (current: {State}), skipping execution",
                    instanceId,
                    instance.State);
                return;
            }

            var definition = await cacheService.GetDefinitionAsync(instance.JobKey);
            if (definition == null)
            {
                logger.LogError("Job definition not found for {InstanceId}", instanceId);
                await jobInstanceManager.UpdateStateAsync(
                    instanceId,
                    JobState.Failed,
                    "Job definition not found");
                return;
            }

            // Transition to Enqueued
            await jobInstanceManager.UpdateStateAsync(
                instanceId,
                JobState.Enqueued,
                "Delay expired");

            logger.LogInformation("Delayed job ready: {JobKey}, {InstanceId}",
                instance.JobKey, instanceId);

            // Dispatch for execution (JobArgs is already JSON string)
            await jobDispatcher.PublishJobExecutionEventAsync(instance, definition, instance.JobArgs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in timer callback for {InstanceId}", instanceId);
            try
            {
                await jobInstanceManager.UpdateStateAsync(
                    instanceId,
                    JobState.Failed,
                    $"Timer callback error: {ex}");
            }
            catch { /* Best effort */ }
        }
    }

    /// <summary>
    /// Handles JobCancellationRequestedEvent to cancel scheduled jobs.
    /// </summary>
    private async Task OnJobCancellationRequestedAsync(JobCancellationRequestedEvent evt)
    {
        if (!string.Equals(evt.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            logger.LogDebug("Ignored JobCancellationRequestedEvent for foreign scope {Scope}", evt.SchedulerScopeKey);
            return;
        }

        try
        {
            // Remove and dispose timer if exists
            if (_inFlightDelayedSchedules.TryRemove(evt.InstanceId, out var schedule))
            {
                schedule.Timer?.Dispose();
                logger.LogInformation("Cancelled delayed job timer for {InstanceId}", evt.InstanceId);
            }

            // Update instance state to Cancelled
            await jobInstanceManager.UpdateStateAsync(
                evt.InstanceId,
                JobState.Cancelled,
                "Cancelled by user request");

            logger.LogInformation("Cancelled scheduled job {InstanceId}", evt.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling scheduled job {InstanceId}", evt.InstanceId);
        }
    }

    /// <summary>
    /// Stops the triggered job scheduler gracefully, disposing all timers and subscriptions.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("TriggeredJobScheduler stopping...");

        // Unsubscribe from events
        if (_triggeredJobSubscription != null)
        {
            await _triggeredJobSubscription.DisposeAsync();
        }
        if (_cancellationSubscription != null)
        {
            await _cancellationSubscription.DisposeAsync();
        }
        if (_manualExecutionSubscription != null)
        {
            await _manualExecutionSubscription.DisposeAsync();
        }

        // Dispose all delayed job timers
        foreach (var schedule in _inFlightDelayedSchedules.Values)
        {
            schedule.Timer?.Dispose();
        }
        _inFlightDelayedSchedules.Clear();

        logger.LogInformation("TriggeredJobScheduler stopped");
    }
}
