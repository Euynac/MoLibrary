using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Manages scheduling and execution of recurring jobs based on cron expressions.
/// Handles dynamic schedule updates when job definitions change.
/// </summary>
public class RecurringJobScheduler(
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    RecurringJobValidator validator,
    ILogger<RecurringJobScheduler> logger)
{
    // Recurring job scheduling state
    private readonly ConcurrentDictionary<string, RecurringJobSchedule> _inFlightRecurringSchedules = new();

    // Synchronization for updating schedules
    private readonly SemaphoreSlim _scheduleLock = new(1, 1);

    /// <summary>
    /// Initializes the recurring job scheduler.
    /// Loads and schedules all recurring job definitions.
    /// </summary>
    /// <param name="debugMode">If true, jobs will not be automatically scheduled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeAsync(bool debugMode, CancellationToken cancellationToken = default)
    {
        if (debugMode)
        {
            logger.LogWarning("RecurringJobDebugMode is enabled. Jobs will not be automatically scheduled.");
            return;
        }

        // Load all recurring job definitions from cache
        var allDefinitions = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);
        var recurringJobs = allDefinitions.Where(d => d.JobType == JobType.Recurring).ToList();

        logger.LogInformation("Loaded {Count} recurring job definitions", recurringJobs.Count);

        // Schedule each recurring task
        foreach (var definition in recurringJobs)
        {
            ScheduleRecurringJob(definition);
        }
    }

    /// <summary>
    /// Handles JobDefinitionsChangedEvent to update in-flight schedules dynamically.
    /// </summary>
    public async Task OnJobDefinitionsChangedAsync(JobDefinitionsChangedEvent evt)
    {
        await _scheduleLock.WaitAsync();
        try
        {
            logger.LogInformation(
                "Received JobDefinitionsChangedEvent from {FromProject}: {AddedCount} added, {UpdatedCount} updated, {DeletedCount} deleted",
                evt.FromProject,
                evt.AddedJobKeys.Count,
                evt.UpdatedJobKeys.Count,
                evt.DeletedJobKeys.Count);

            // 1. Remove schedules for deleted jobs
            foreach (var deletedJobKey in evt.DeletedJobKeys)
            {
                logger.LogInformation("Removed schedule for deleted job: {JobKey}", deletedJobKey);
                await RemoveSchedule(deletedJobKey);
            }

            // 2. Handle updated recurring jobs
            var updatedRecurringJobs = evt.UpdatedDefinitions
                .Where(d => d.JobType == JobType.Recurring)
                .ToList();
            
            // 3. Get current recurring job definitions
            var addedRecurringJobs = evt.AddedDefinitions
                .Where(d => d.JobType == JobType.Recurring)
                .ToList();

            // 4. Update or add schedules for current recurring jobs
            foreach (var jobDefinition in addedRecurringJobs.CombineForeach(updatedRecurringJobs))
            {
                if (_inFlightRecurringSchedules.ContainsKey(jobDefinition.JobKey))
                {
                    await RemoveSchedule(jobDefinition.JobKey);
                    logger.LogInformation("Removed schedule for updated job: {JobKey}", jobDefinition.JobKey);
                }
                ScheduleRecurringJob(jobDefinition);
            }

            logger.LogInformation(
                "JobDefinitionsChangedEvent completed. Scheduled {AddedCount} added jobs, {UpdatedCount} updated jobs",
                addedRecurringJobs.Count,
                updatedRecurringJobs.Count);
        }
        finally
        {
            _scheduleLock.Release();
        }
    }

    /// <summary>
    /// Schedules a recurring job using its cron expression.
    /// Thread-safe: Disposes old timer before creating new one to prevent leaks.
    /// </summary>
    /// <param name="definition">Job definition to schedule</param>
    /// <param name="lastOccurrence">Last occurrence time (used when rescheduling to ensure we get the next occurrence)</param>
    private async void ScheduleRecurringJob(JobDefinition definition, DateTime? lastOccurrence = null)
    {
        try
        {
            var validatedDefinition = definition;
            if (lastOccurrence == null)
            {
                // Validate the job definition before scheduling
                validatedDefinition = await validator.ValidateRecurringJobAsync(
                    definition,
                    RemoveSchedule,
                    def =>
                    {
                        ScheduleRecurringJob(def);
                        return Task.CompletedTask;
                    });

                if (validatedDefinition == null)
                {
                    return;
                }
            }
          

            // Parse cron expression (with seconds support)
            var cronExpression = CronExpression.Parse(validatedDefinition.CronExpression, CronFormat.IncludeSeconds);

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
                    validatedDefinition.JobKey,
                    validatedDefinition.CronExpression);
                return;
            }

            var dueTime = nextOccurrence.Value - now + TimeSpan.FromMilliseconds(1);

            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }

            // Create timer for next occurrence
            var timer = new Timer(
                _ => OnRecurringJobTimerCallback(validatedDefinition.JobKey, nextOccurrence.Value),
                null,
                dueTime,
                Timeout.InfiniteTimeSpan); // One-shot timer

            var schedule = new RecurringJobSchedule
            {
                JobKey = validatedDefinition.JobKey,
                CronExpression = cronExpression,
                Timer = timer,
                NextOccurrence = nextOccurrence.Value
            };

            _inFlightRecurringSchedules[validatedDefinition.JobKey] = schedule;

            logger.LogDebug(
                "Scheduled recurring job {JobKey}, next execution at {NextExecution} (in {DueTime})",
                validatedDefinition.JobKey,
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

    /// <summary>
    /// Timer callback for recurring job execution.
    /// </summary>
    /// <param name="jobKey">Job key</param>
    /// <param name="scheduledOccurrence">The occurrence time this timer was scheduled for</param>
    // ReSharper disable once AsyncVoidMethod
    private async void OnRecurringJobTimerCallback(string jobKey, DateTime scheduledOccurrence)
    {
        Thread.Sleep(1);
        try
        {
            var definition = await validator.GetValidatedRecurringJobAsync(
                jobKey,
                RemoveSchedule,
                def =>
                {
                    ScheduleRecurringJob(def); // Reschedule if before start time
                    return Task.CompletedTask;
                });

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
    /// Removes a scheduled recurring job.
    /// </summary>
    private async Task RemoveSchedule(string jobKey)
    {
        if (_inFlightRecurringSchedules.TryRemove(jobKey, out var oldSchedule) && oldSchedule.Timer is { } timer)
        {
            await timer.DisposeAsync();
        }
    }

    /// <summary>
    /// Stops the recurring job scheduler gracefully, disposing all timers.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("RecurringJobScheduler stopping...");

        // Dispose all recurring job timers
        foreach (var schedule in _inFlightRecurringSchedules.Values)
        {
            schedule.Timer?.Dispose();
        }
        _inFlightRecurringSchedules.Clear();

        // Dispose lock
        _scheduleLock.Dispose();

        logger.LogInformation("RecurringJobScheduler stopped");

        await Task.CompletedTask;
    }
}
