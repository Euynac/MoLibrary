using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Manages scheduling and execution of recurring jobs based on cron expressions.
/// Handles dynamic schedule updates when job definitions change.
/// </summary>
public class RecurringJobScheduler(
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    RecurringJobValidator validator,
    IOptions<ModuleJobSchedulerOption> options,
    IOptions<ModuleClockOption> clockOptions,
    ILogger<RecurringJobScheduler> logger)
{
    private readonly ModuleJobSchedulerOption _options = options.Value;
    private readonly TimeZoneInfo _cronTimeZone = clockOptions.Value.ConfiguredTimeZone ?? TimeZoneInfo.Local;

    // Recurring job scheduling state
    private readonly ConcurrentDictionary<string, RecurringJobSchedule> _inFlightRecurringSchedules = new();

    // Long-interval job tracking (for jobs exceeding Timer threshold)
    private readonly ConcurrentDictionary<string, LongIntervalRecurringSchedule> _longIntervalSchedules = new();

    // Synchronization for updating schedules
    private readonly SemaphoreSlim _scheduleLock = new(1, 1);

    /// <summary>
    /// Initializes the recurring job scheduler.
    /// Loads and schedules all recurring job definitions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_options.RecurringJobDebugMode)
        {
            logger.LogWarning("RecurringJobDebugMode is enabled. Jobs will not be automatically scheduled.");
            return;
        }

        logger.LogInformation(
            "Recurring cron evaluation timezone: {TimeZoneId} (Display: {DisplayName}, BaseUtcOffset: {BaseUtcOffset})",
            _cronTimeZone.Id,
            _cronTimeZone.DisplayName,
            _cronTimeZone.BaseUtcOffset);

        // Load all recurring job definitions from cache
        var allDefinitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
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
        if (!string.Equals(evt.SchedulerScopeKey, _options.SchedulerScopeKey, StringComparison.Ordinal))
        {
            logger.LogDebug("Ignored JobDefinitionsChangedEvent for foreign scope {Scope}", evt.SchedulerScopeKey);
            return;
        }

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
          

            if (string.IsNullOrWhiteSpace(validatedDefinition.CronExpression))
            {
                logger.LogWarning(
                    "Recurring job {JobKey} has empty cron expression after validation. Skipping scheduling.",
                    validatedDefinition.JobKey);
                return;
            }

            // Parse cron expression (supports both 5-segment standard and 6-segment with seconds)
            var cronExpression = CronHelper.Parse(validatedDefinition.CronExpression);

            // Calculate next occurrence with minimum buffer to avoid timer accumulation
            // IMPORTANT: Add 100ms buffer to current time to prevent dueTime from being too small
            var now = DateTime.UtcNow;
            var baseTime = now.AddMilliseconds(100);
            var nextOccurrence = cronExpression.GetNextOccurrence(baseTime, _cronTimeZone);

            // If rescheduling and next occurrence is same as last, advance by 1ms to get exact next occurrence
            if (lastOccurrence.HasValue && nextOccurrence.HasValue && nextOccurrence.Value == lastOccurrence.Value)
            {
                nextOccurrence = cronExpression.GetNextOccurrence(lastOccurrence.Value.AddMilliseconds(1), _cronTimeZone);
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

            // Check if interval exceeds Timer safety threshold
            var timerThreshold = TimeSpan.FromDays(_options.TimerSafetyThresholdDays);
            if (dueTime > timerThreshold)
            {
                logger.LogInformation(
                    "Recurring job {JobKey} interval ({Days} days) exceeds timer threshold ({ThresholdDays} days). Deferring to long-interval scheduler. Next execution: {NextTime}",
                    validatedDefinition.JobKey,
                    dueTime.TotalDays,
                    _options.TimerSafetyThresholdDays,
                    nextOccurrence.Value);

                // Record to memory dictionary, handled by LongIntervalScheduler
                RecordLongIntervalSchedule(validatedDefinition.JobKey, nextOccurrence.Value);
                return;
            }

            // If the job was previously tracked as long-interval, switch it back to timer mode.
            _longIntervalSchedules.TryRemove(validatedDefinition.JobKey, out _);

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
                JobState.Enqueued,
                initDescription: "Recurring scheduler timer callback");

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

        // Also remove from long-interval tracking
        _longIntervalSchedules.TryRemove(jobKey, out _);
    }

    /// <summary>
    /// Records a long-interval recurring job in memory.
    /// Used when cron interval exceeds Timer safety threshold.
    /// </summary>
    private void RecordLongIntervalSchedule(string jobKey, DateTime nextOccurrence)
    {
        // Remove existing Timer (if any)
        if (_inFlightRecurringSchedules.TryRemove(jobKey, out var existingSchedule))
        {
            existingSchedule.Timer?.Dispose();
            logger.LogDebug("Removed existing timer for long-interval job {JobKey}", jobKey);
        }

        // Record to memory dictionary
        var schedule = new LongIntervalRecurringSchedule
        {
            JobKey = jobKey,
            NextScheduledTime = nextOccurrence,
            CalculatedAt = DateTime.UtcNow
        };

        _longIntervalSchedules.AddOrUpdate(jobKey, schedule, (_, _) => schedule);

        logger.LogDebug(
            "Recorded long-interval schedule for {JobKey}. Next execution: {NextTime}",
            jobKey,
            nextOccurrence);
    }

    /// <summary>
    /// Gets all long-interval recurring schedules for scanning.
    /// Called by LongIntervalSchedulerService.
    /// </summary>
    internal IReadOnlyCollection<LongIntervalRecurringSchedule> GetLongIntervalSchedules()
    {
        return _longIntervalSchedules.Values.ToList();
    }

    /// <summary>
    /// Transitions a long-interval recurring job to Timer mode.
    /// Called by LongIntervalSchedulerService when job is close to execution time.
    /// </summary>
    internal async Task TransitionToTimerModeAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (!_longIntervalSchedules.TryGetValue(jobKey, out var schedule))
        {
            logger.LogWarning(
                "Attempted to transition {JobKey} to Timer mode but it's not in long-interval tracking",
                jobKey);
            return;
        }

        // Get Definition
        var definition = await cacheService.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            logger.LogWarning(
                "Definition not found for long-interval job {JobKey}, removing from tracking",
                jobKey);
            _longIntervalSchedules.TryRemove(jobKey, out _);
            return;
        }

        logger.LogInformation(
            "Transitioning {JobKey} from long-interval to Timer mode. Execution in {Minutes} minutes",
            jobKey,
            (schedule.NextScheduledTime - DateTime.UtcNow).TotalMinutes);

        // Remove from long-interval dictionary (ScheduleRecurringJob will automatically use Timer based on interval)
        _longIntervalSchedules.TryRemove(jobKey, out _);

        // Reschedule (will automatically use Timer mode because interval is now <24 days)
        ScheduleRecurringJob(definition, lastOccurrence: null);
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
        _longIntervalSchedules.Clear();

        // Dispose lock
        _scheduleLock.Dispose();

        logger.LogInformation("RecurringJobScheduler stopped");

        await Task.CompletedTask;
    }
}
