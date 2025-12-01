using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Manages triggered and delayed job scheduling.
/// Handles job triggering events, delayed execution, cancellation, and recovery on restart.
/// </summary>
public class TriggeredJobScheduler(
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    IMoJobScheduleMetadataStore metadataStore,
    DelayedJobRecoveryService recoveryService,
    ILogger<TriggeredJobScheduler> logger)
{
    // Delayed triggered job scheduling state
    private readonly ConcurrentDictionary<string, DelayedJobSchedule> _inFlightDelayedSchedules = new();

    // Event subscriptions
    private IDisposable? _triggeredJobSubscription;
    private IDisposable? _cancellationSubscription;

    /// <summary>
    /// Initializes the triggered job scheduler.
    /// Subscribes to events and recovers scheduled jobs.
    /// </summary>
    /// <param name="eventBus">Event bus for job events</param>
    /// <param name="debugMode">If true, skips automatic recovery</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeAsync(IMoEventBus eventBus, bool debugMode, CancellationToken cancellationToken = default)
    {
        // Subscribe to job triggered event
        _triggeredJobSubscription = eventBus.Subscribe<JobTriggeredEvent>(OnJobTriggeredAsync);
        logger.LogDebug("Subscribed to JobTriggeredEvent");

        // Subscribe to job cancellation event
        _cancellationSubscription = eventBus.Subscribe<JobCancellationRequestedEvent>(OnJobCancellationRequestedAsync);
        logger.LogDebug("Subscribed to JobCancellationRequestedEvent");

        if (!debugMode)
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
    }

    /// <summary>
    /// Handles JobTriggeredEvent to create and schedule triggered jobs.
    /// </summary>
    private async Task OnJobTriggeredAsync(JobTriggeredEvent evt)
    {
        try
        {
            var definition = await cacheService.GetJobDefinitionAsync(evt.JobKey);
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
                instanceId: evt.InstanceId);

            if (scheduledTime.HasValue)
            {
                instance.ScheduledExecutionTime = scheduledTime.Value;
                await metadataStore.SaveJobInstanceAsync(instance);
            }

            logger.LogInformation(
                "Created job instance: {JobKey}, {InstanceId}, State: {State}, Scheduled: {Time}",
                evt.JobKey,
                instance.InstanceId,
                initialState,
                scheduledTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Immediate");

            if (evt.Delay.HasValue)
            {
                ScheduleDelayedJob(instance, definition, scheduledTime!.Value);
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
    /// Schedules a delayed job using a one-shot timer.
    /// </summary>
    private void ScheduleDelayedJob(JobInstance instance, JobDefinition definition, DateTime scheduledTime)
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

            var instance = await metadataStore.GetJobInstanceAsync(instanceId);
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

            var definition = await cacheService.GetJobDefinitionAsync(instance.JobKey);
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
                    $"Timer callback error: {ex.Message}");
            }
            catch { /* Best effort */ }
        }
    }

    /// <summary>
    /// Handles JobCancellationRequestedEvent to cancel scheduled jobs.
    /// </summary>
    private async Task OnJobCancellationRequestedAsync(JobCancellationRequestedEvent evt)
    {
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
        _triggeredJobSubscription?.Dispose();
        _cancellationSubscription?.Dispose();

        // Dispose all delayed job timers
        foreach (var schedule in _inFlightDelayedSchedules.Values)
        {
            schedule.Timer?.Dispose();
        }
        _inFlightDelayedSchedules.Clear();

        logger.LogInformation("TriggeredJobScheduler stopped");

        await Task.CompletedTask;
    }
}
