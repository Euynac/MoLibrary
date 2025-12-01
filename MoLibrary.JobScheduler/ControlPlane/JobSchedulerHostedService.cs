using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Core;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.RegisterCentre.Interfaces;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Central orchestrator for job scheduling and execution requests.
/// Extends CoordinatedLeaderService for consistent initialization with RegisterCentre coordination and leader-only execution.
/// Manages recurring job scheduling via cron expressions and coordinates triggered job execution.
/// Listens to JobDefinitionsChangedEvent to dynamically update schedules when definitions change.
/// </summary>
public class JobSchedulerHostedService(
    IOptions<ModuleJobSchedulerOption> options,
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    ILeaderService leaderService,
    ILogger<JobSchedulerHostedService> logger,
    IServiceRegistrationCoordinator coordinator,
    IMoJobScheduleMetadataStore metadataStore) : CoordinatedLeaderService(leaderService, options, logger, coordinator)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    // Recurring job scheduling state
    private readonly ConcurrentDictionary<string, RecurringJobSchedule> _inFlightRecurringSchedules = new();

    // Delayed triggered job scheduling state
    private readonly ConcurrentDictionary<string, DelayedJobSchedule> _inFlightDelayedSchedules = new();

    // Synchronization for updating schedules
    private readonly SemaphoreSlim _scheduleLock = new(1, 1);

    // Event subscriptions
    private IDisposable? _definitionsChangedSubscription;
    private IDisposable? _triggeredJobSubscription;

    protected override string ServiceName => nameof(JobSchedulerHostedService);

    protected override async Task InitializeServiceAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "JobScheduler starting. RecurringJobDebugMode: {RecurringDebug}, TriggeredJobDebugMode: {TriggeredDebug}",
            _options.RecurringJobDebugMode,
            _options.TriggeredJobDebugMode);

        // Load all recurring job definitions from cache
        var allDefinitions = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);
        var recurringJobs = allDefinitions.Where(d => d.JobType == JobType.Recurring).ToList();

        logger.LogInformation("Loaded {Count} recurring job definitions", recurringJobs.Count);

        if (_options.RecurringJobDebugMode)
        {
            logger.LogWarning("RecurringJobDebugMode is enabled. Jobs will not be automatically scheduled.");
            return;
        }

        // Schedule each recurring task
        foreach (var definition in recurringJobs)
        {
            ScheduleRecurringJob(definition);
        }

        // Subscribe to job definitions changed event
        _definitionsChangedSubscription = eventBus.Subscribe<JobDefinitionsChangedEvent>(OnJobDefinitionsChangedAsync);
        logger.LogDebug("Subscribed to JobDefinitionsChangedEvent");

        // Subscribe to job triggered event
        _triggeredJobSubscription = eventBus.Subscribe<JobTriggeredEvent>(OnJobTriggeredAsync);
        logger.LogDebug("Subscribed to JobTriggeredEvent");

        // Subscribe to job cancellation event
        eventBus.Subscribe<JobCancellationRequestedEvent>(OnJobCancellationRequestedAsync);
        logger.LogDebug("Subscribed to JobCancellationRequestedEvent");

        // Recover and reschedule delayed jobs
        await RecoverScheduledJobsAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the job scheduler gracefully, cancelling all pending timers.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobScheduler stopping...");

        try
        {
            // Call base to stop ExecuteAsync
            await base.StopAsync(cancellationToken);

            // Unsubscribe from events
            _definitionsChangedSubscription?.Dispose();
            _triggeredJobSubscription?.Dispose();

            // Dispose all recurring job timers
            foreach (var schedule in _inFlightRecurringSchedules.Values)
            {
                schedule.Timer?.Dispose();
            }
            _inFlightRecurringSchedules.Clear();

            // Dispose all delayed job timers
            foreach (var schedule in _inFlightDelayedSchedules.Values)
            {
                schedule.Timer?.Dispose();
            }
            _inFlightDelayedSchedules.Clear();

            // Dispose lock
            _scheduleLock.Dispose();

            logger.LogInformation("JobScheduler stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JobScheduler shutdown");
            throw;
        }
    }
    
    /// <summary>
    /// Schedules a recurring job using its cron expression.
    /// Thread-safe: Disposes old timer before creating new one to prevent leaks.
    /// </summary>
    /// <param name="definition">Job definition to schedule</param>
    /// <param name="lastOccurrence">Last occurrence time (used when rescheduling to ensure we get the next occurrence)</param>
    private void ScheduleRecurringJob(JobDefinition definition, DateTime? lastOccurrence = null)
    {
        try
        {
            // Parse cron expression (with seconds support)
            var cronExpression = CronExpression.Parse(definition.CronExpression, CronFormat.IncludeSeconds);

            // Calculate next occurrence with minimum buffer to avoid timer accumulation
            // IMPORTANT: Add 100ms buffer to current time to prevent dueTime from being too small
            var now = DateTime.UtcNow;
            var baseTime = now.AddMilliseconds(100);
            var nextOccurrence = cronExpression.GetNextOccurrence(baseTime, TimeZoneInfo.Utc);

            // If rescheduling and next occurrence is same as last, advance by 1ms to get exact next occurrence
            if (lastOccurrence.HasValue && nextOccurrence.HasValue && nextOccurrence.Value == lastOccurrence.Value)
            {
                nextOccurrence = cronExpression.GetNextOccurrence(lastOccurrence.Value.AddMilliseconds(1), TimeZoneInfo.Utc);
            }

            if (!nextOccurrence.HasValue)
            {
                logger.LogWarning(
                    "Cron expression for job {JobKey} has no future occurrences: {CronExpression}",
                    definition.JobKey,
                    definition.CronExpression);
                return;
            }

            var dueTime = nextOccurrence.Value - now + TimeSpan.FromMilliseconds(1);
            
            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }


            // Ensure minimum delay to prevent timer accumulation
            // Create timer for next occurrence
            var timer = new Timer(
                _ => OnRecurringJobTimerCallback(definition.JobKey, nextOccurrence.Value),
                null,
                dueTime,
                Timeout.InfiniteTimeSpan); // One-shot timer

            var schedule = new RecurringJobSchedule
            {
                JobKey = definition.JobKey,
                CronExpression = cronExpression,
                Timer = timer,
                NextOccurrence = nextOccurrence.Value
            };

            _inFlightRecurringSchedules[definition.JobKey] = schedule;

            logger.LogDebug(
                "Scheduled recurring job {JobKey}, next execution at {NextExecution} (in {DueTime})",
                definition.JobKey,
                nextOccurrence.Value,
                dueTime);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to schedule recurring job {JobKey}: {Message}",
                definition.JobKey,
                ex.Message);
        }
    }

    public async Task<JobDefinition?> GetValidatedRecurringJobAsync(string jobKey)
    {
        
        var definition = await cacheService.GetJobDefinitionAsync(jobKey);
        if (definition == null)
        {
            logger.LogWarning(
                "Recurring job {JobKey} not found during timer callback. Removing from schedule.",
                jobKey);
            await RemoveSchedule(jobKey);
            return null;
        }
        
        if (string.IsNullOrWhiteSpace(definition.CronExpression))
        {
            logger.LogWarning(
                "Recurring job {JobKey} has no cron expression. Skipping automatic scheduling.",
                definition.JobKey);
            await RemoveSchedule(jobKey);
            return null;
        }

        if (definition.IsDisabled)
        {
            logger.LogWarning(
                "Recurring job {JobKey} is disabled. Skipping scheduling.",
                definition.JobKey);
            await RemoveSchedule(jobKey);
            return null;
        }

        // Check if job is still enabled
        if (definition.IsDisabled)
        {
            logger.LogDebug(
                "Recurring job {JobKey} is disabled. Skipping schedule.",
                jobKey);
            await RemoveSchedule(jobKey);
            return null;
        }

        var now = DateTime.UtcNow;

        // Check start/end time constraints
        if (definition.StartTime.HasValue && now < definition.StartTime.Value)
        {
            logger.LogDebug(
                "Recurring job {JobKey} schedule skipped (before start time {StartTime})",
                jobKey,
                definition.StartTime.Value);

            // Reschedule for next occurrence
            ScheduleRecurringJob(definition);//TODO 优化为Delayed Timer
            await RemoveSchedule(jobKey);
            return null;
        }

        if (definition.EndTime.HasValue && now > definition.EndTime.Value)
        {
            logger.LogInformation(
                "Recurring job {JobKey} has passed end time {EndTime}. Removing from schedule.",
                jobKey,
                definition.EndTime.Value);
            await RemoveSchedule(jobKey);
            return null;
        }

        return definition;

       
    }
    private async Task RemoveSchedule(string jobKey)
    {
        if (_inFlightRecurringSchedules.TryRemove(jobKey, out var oldSchedule) && oldSchedule.Timer is { } timer)
        {
            await timer.DisposeAsync();
        }
    }
    /// <summary>
    /// Handles JobDefinitionsChangedEvent to update in-flight schedules dynamically.
    /// </summary>
    private async Task OnJobDefinitionsChangedAsync(JobDefinitionsChangedEvent evt)
    {
   
        await _scheduleLock.WaitAsync();
        try
        {
            logger.LogInformation(
                "Received JobDefinitionsChangedEvent from {FromProject}: {AddedCount} added, {DeletedCount} deleted, {TotalCount} total definitions",
                evt.FromProject,
                evt.AddedJobKeys.Count,
                evt.DeletedJobKeys.Count,
                evt.AddedDefinitions.Count);

            // 1. Remove schedules for deleted jobs
            foreach (var deletedJobKey in evt.DeletedJobKeys)
            {
                logger.LogInformation("Removed schedule for deleted job: {JobKey}", deletedJobKey);
                await RemoveSchedule(deletedJobKey);
            }
            
             // 2. Get current recurring job definitions
            var recurringJobs = evt.AddedDefinitions
                .Where(d => d.JobType == JobType.Recurring)
                .ToList();

            // 3. Update or add schedules for current recurring jobs
            foreach (var jobDefinition in recurringJobs)
            {
                if (_inFlightRecurringSchedules.ContainsKey(jobDefinition.JobKey))
                {
                    await RemoveSchedule(jobDefinition.JobKey);
                    logger.LogInformation("Removed schedule for updated job: {JobKey}", jobDefinition.JobKey);
                }
                ScheduleRecurringJob(jobDefinition);
            }
            logger.LogInformation("JobDefinitionsChangedEvent completed. Scheduled {Count} new recurring jobs", recurringJobs.Count);
        }
        finally
        {
            _scheduleLock.Release();
        }
    }

    /// <summary>
    /// Timer callback for recurring job execution.
    /// </summary>
    /// <param name="jobKey">Job key</param>
    /// <param name="scheduledOccurrence">The occurrence time this timer was scheduled for</param>
    private async void OnRecurringJobTimerCallback(string jobKey, DateTime scheduledOccurrence)
    {
        Thread.Sleep(1);
        try
        {
            var definition = await GetValidatedRecurringJobAsync(jobKey);
            if (definition == null)
            {
                return;
            }

            // Create instance and publish event
            var instance = await jobInstanceManager.CreateInstanceAsync(
                definition,
                parameters: null,
                JobState.Enqueued);

            logger.LogDebug(
                "Recurring job triggered: {JobKey}, InstanceId: {InstanceId}",
                jobKey,
                instance);

            await jobDispatcher.PublishJobExecutionEventAsync(instance, definition, null);

            // Reschedule for next occurrence, passing the scheduled occurrence to ensure we get the next one
            ScheduleRecurringJob(definition, scheduledOccurrence);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in recurring job timer callback for {JobKey}: {Message}",
                jobKey,
                ex.Message);
            //TODO Handle rescheduling
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
    /// Recovers scheduled jobs from metadata store on service restart.
    /// </summary>
    private async Task RecoverScheduledJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Recovering scheduled jobs...");

            var scheduledInstances = await metadataStore.GetJobInstancesAsync(
                jobKey: null,
                stateFilter: JobState.Scheduled,
                pageNumber: 1,
                pageSize: 10000,
                cancellationToken: cancellationToken);

            if (scheduledInstances.Count == 0)
            {
                logger.LogInformation("No scheduled jobs to recover");
                return;
            }

            logger.LogInformation("Recovering {Count} scheduled job(s)", scheduledInstances.Count);

            foreach (var instance in scheduledInstances)
            {
                if (!instance.ScheduledExecutionTime.HasValue)
                {
                    logger.LogWarning(
                        "Instance {InstanceId} missing ScheduledExecutionTime, marking as Failed",
                        instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Failed,
                        "Missing ScheduledExecutionTime during recovery",
                        cancellationToken);
                    continue;
                }

                var definition = await cacheService.GetJobDefinitionAsync(instance.JobKey, cancellationToken);
                if (definition == null)
                {
                    logger.LogWarning("Job definition not found for {InstanceId}", instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Failed,
                        "Job definition not found during recovery",
                        cancellationToken);
                    continue;
                }

                if (definition.IsDisabled)
                {
                    logger.LogInformation("Job disabled, cancelling {InstanceId}", instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Cancelled,
                        "Job disabled during recovery",
                        cancellationToken);
                    continue;
                }

                ScheduleDelayedJob(instance, definition, instance.ScheduledExecutionTime.Value);
                logger.LogInformation("Recovered {InstanceId}, scheduled for {Time}",
                    instance.InstanceId,
                    instance.ScheduledExecutionTime.Value);
            }

            logger.LogInformation("Scheduled job recovery completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recovering scheduled jobs");
        }
    }


    /// <summary>
    /// Internal class to track delayed job scheduling state.
    /// </summary>
    private class DelayedJobSchedule
    {
        public required string InstanceId { get; init; }
        public required string JobKey { get; init; }
        public Timer? Timer { get; set; }
        public DateTime ScheduledExecutionTime { get; set; }
    }

    /// <summary>
    /// Internal class to track recurring job scheduling state.
    /// </summary>
    private class RecurringJobSchedule
    {
        public required string JobKey { get; init; }
        public required CronExpression CronExpression { get; init; }
        public Timer? Timer { get; set; }
        public DateTime NextOccurrence { get; set; }
    }
}
