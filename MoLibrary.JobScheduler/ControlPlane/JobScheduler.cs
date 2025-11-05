using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Central orchestrator for job scheduling and execution requests.
/// Implements IHostedService to manage recurring job scheduling via cron expressions
/// and coordinates triggered job execution with optional delays.
/// </summary>
public class MoJobScheduler(
    IOptions<ModuleJobSchedulerOption> options,
    IMoJobScheduleMetadataStore metadataStore,
    JobRegistry jobRegistry,
    MetadataWriter metadataWriter,
    IMoEventBus eventBus,
    ILogger<MoJobScheduler> logger) : IHostedService
{
    private readonly ModuleJobSchedulerOption _options = options.Value;

    // Recurring job scheduling state
    private readonly ConcurrentDictionary<string, RecurringJobSchedule> _recurringSchedules = new();
    private readonly ConcurrentDictionary<string, Timer> _delayedJobTimers = new();
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Starts the job scheduler, loading all recurring jobs and scheduling cron-based executions.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = new CancellationTokenSource();

        logger.LogInformation(
            "JobScheduler starting. RecurringJobDebugMode: {RecurringDebug}, TriggeredJobDebugMode: {TriggeredDebug}",
            _options.RecurringJobDebugMode,
            _options.TriggeredJobDebugMode);

        // Load all recurring job definitions
        var allDefinitions = await metadataStore.GetAllJobDefinitionsAsync(cancellationToken);
        var recurringJobs = allDefinitions.Where(d => d.Type == JobType.Recurring).ToList();

        logger.LogInformation("Loaded {Count} recurring job definitions", recurringJobs.Count);

        // Schedule each recurring task
        foreach (var definition in recurringJobs)
        {
            try
            {
                ScheduleRecurringJob(definition);
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

        logger.LogInformation("JobScheduler started successfully");
    }

    /// <summary>
    /// Stops the job scheduler gracefully, cancelling all pending timers.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("JobScheduler stopping...");

        _stoppingCts?.Cancel();

        // Dispose all recurring job timers
        foreach (var schedule in _recurringSchedules.Values)
        {
            schedule.Timer?.Dispose();
        }
        _recurringSchedules.Clear();

        // Dispose all delayed job timers
        foreach (var timer in _delayedJobTimers.Values)
        {
            timer?.Dispose();
        }
        _delayedJobTimers.Clear();

        _stoppingCts?.Dispose();

        logger.LogInformation("JobScheduler stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a triggered job for execution with optional delay.
    /// </summary>
    /// <typeparam name="TJob">The job type inheriting from TriggeredJob{TParam}.</typeparam>
    /// <param name="parameters">Parameters to pass to the job.</param>
    /// <param name="delay">Optional delay before job execution. If null, job executes immediately.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The instance ID of the created job instance.</returns>
    public async Task<string> EnqueueAsync<TJob>(
        object? parameters = null,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default) where TJob : class
    {
        var jobKey = typeof(TJob).FullName ?? typeof(TJob).Name;

        // Get job definition
        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Job {jobKey} is not registered. Ensure the job class inherits from TriggeredJob<TParam> and is discovered during module initialization.");
        }

        // Determine initial state and scheduled time
        JobState initialState;
        DateTime? scheduledFor = null;

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            initialState = JobState.Scheduled;
            scheduledFor = DateTime.UtcNow.Add(delay.Value);
        }
        else
        {
            initialState = JobState.Enqueued;
        }

        // Create instance
        var instanceId = await metadataWriter.CreateInstanceAsync(
            definition,
            parameters,
            initialState,
            scheduledFor,
            cancellationToken);

        logger.LogInformation(
            "Triggered job enqueued: {JobKey}, InstanceId: {InstanceId}, Delay: {Delay}, InitialState: {InitialState}",
            jobKey,
            instanceId,
            delay,
            initialState);

        // If delayed, schedule timer for state transition
        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            ScheduleDelayedJob(instanceId, jobKey, delay.Value);
        }
        else if (!_options.TriggeredJobDebugMode)
        {
            // Immediate execution - publish event (unless debug mode is enabled)
            await PublishJobExecutionEventAsync(instanceId, jobKey, parameters, cancellationToken);
        }
        else
        {
            logger.LogDebug(
                "TriggeredJobDebugMode is enabled. Job {JobKey} instance {InstanceId} created but not published for automatic execution.",
                jobKey,
                instanceId);
        }

        return instanceId;
    }

    /// <summary>
    /// Pauses a recurring job by marking it as disabled in the metadata store.
    /// </summary>
    /// <param name="jobKey">The unique job key to pause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PauseRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Job {jobKey} not found");
        }

        if (definition.Type != JobType.Recurring)
        {
            throw new InvalidOperationException($"Job {jobKey} is not a recurring job");
        }

        // Update definition in metadata store
        definition.IsDisabled = true;
        await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);

        // Cancel timer if exists
        if (_recurringSchedules.TryRemove(jobKey, out var schedule))
        {
            schedule.Timer?.Dispose();
        }

        logger.LogInformation("Recurring job paused: {JobKey}", jobKey);
    }

    /// <summary>
    /// Resumes a paused recurring job by marking it as enabled and rescheduling.
    /// </summary>
    /// <param name="jobKey">The unique job key to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResumeRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Job {jobKey} not found");
        }

        if (definition.Type != JobType.Recurring)
        {
            throw new InvalidOperationException($"Job {jobKey} is not a recurring job");
        }

        // Update definition in metadata store
        definition.IsDisabled = false;
        await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);

        // Reschedule
        ScheduleRecurringJob(definition);

        logger.LogInformation("Recurring job resumed: {JobKey}", jobKey);
    }

    /// <summary>
    /// Schedules a recurring job using its cron expression.
    /// </summary>
    private void ScheduleRecurringJob(JobDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.CronExpression))
        {
            logger.LogDebug(
                "Recurring job {JobKey} has no cron expression. Skipping automatic scheduling.",
                definition.JobKey);
            return;
        }

        if (definition.IsDisabled)
        {
            logger.LogDebug(
                "Recurring job {JobKey} is disabled. Skipping scheduling.",
                definition.JobKey);
            return;
        }

        if (_options.RecurringJobDebugMode)
        {
            logger.LogDebug(
                "RecurringJobDebugMode is enabled. Job {JobKey} will not be automatically scheduled.",
                definition.JobKey);
            return;
        }

        try
        {
            // Parse cron expression (with seconds support)
            var cronExpression = CronExpression.Parse(definition.CronExpression, CronFormat.IncludeSeconds);

            // Calculate next occurrence
            var now = DateTime.UtcNow;
            var nextOccurrence = cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (!nextOccurrence.HasValue)
            {
                logger.LogWarning(
                    "Cron expression for job {JobKey} has no future occurrences: {CronExpression}",
                    definition.JobKey,
                    definition.CronExpression);
                return;
            }

            var dueTime = nextOccurrence.Value - now;
            if (dueTime < TimeSpan.Zero)
            {
                dueTime = TimeSpan.Zero;
            }

            // Create timer for next occurrence
            var timer = new Timer(
                _ => OnRecurringJobTimerCallback(definition.JobKey),
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

            _recurringSchedules[definition.JobKey] = schedule;

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
                "Failed to parse cron expression for job {JobKey}: {CronExpression}",
                definition.JobKey,
                definition.CronExpression);
        }
    }

    /// <summary>
    /// Timer callback for recurring job execution.
    /// </summary>
    private async void OnRecurringJobTimerCallback(string jobKey)
    {
        try
        {
            // Get current job definition (may have been updated)
            var definition = await jobRegistry.GetDefinitionAsync(jobKey);
            if (definition == null)
            {
                logger.LogWarning(
                    "Recurring job {JobKey} not found during timer callback. Removing from schedule.",
                    jobKey);
                _recurringSchedules.TryRemove(jobKey, out _);
                return;
            }

            // Check if job is still enabled
            if (definition.IsDisabled)
            {
                logger.LogDebug(
                    "Recurring job {JobKey} is disabled. Skipping execution.",
                    jobKey);
                _recurringSchedules.TryRemove(jobKey, out var oldSchedule);
                oldSchedule?.Timer?.Dispose();
                return;
            }

            var now = DateTime.UtcNow;

            // Check start/end time constraints
            if (definition.StartTime.HasValue && now < definition.StartTime.Value)
            {
                logger.LogDebug(
                    "Recurring job {JobKey} execution skipped (before start time {StartTime})",
                    jobKey,
                    definition.StartTime.Value);

                // Reschedule for next occurrence
                ScheduleRecurringJob(definition);
                return;
            }

            if (definition.EndTime.HasValue && now > definition.EndTime.Value)
            {
                logger.LogInformation(
                    "Recurring job {JobKey} has passed end time {EndTime}. Removing from schedule.",
                    jobKey,
                    definition.EndTime.Value);

                _recurringSchedules.TryRemove(jobKey, out var oldSchedule);
                oldSchedule?.Timer?.Dispose();
                return;
            }

            // Create instance and publish event
            var instanceId = await metadataWriter.CreateInstanceAsync(
                definition,
                parameters: null,
                JobState.Enqueued,
                scheduledFor: null);

            logger.LogInformation(
                "Recurring job triggered: {JobKey}, InstanceId: {InstanceId}",
                jobKey,
                instanceId);

            await PublishJobExecutionEventAsync(instanceId, jobKey, null);

            // Reschedule for next occurrence
            ScheduleRecurringJob(definition);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in recurring job timer callback for {JobKey}: {Message}",
                jobKey,
                ex.Message);

            // Try to reschedule despite error
            try
            {
                var definition = await jobRegistry.GetDefinitionAsync(jobKey);
                if (definition != null)
                {
                    ScheduleRecurringJob(definition);
                }
            }
            catch (Exception rescheduleEx)
            {
                logger.LogError(
                    rescheduleEx,
                    "Failed to reschedule recurring job {JobKey} after error: {Message}",
                    jobKey,
                    rescheduleEx.Message);
            }
        }
    }

    /// <summary>
    /// Schedules a delayed job timer.
    /// </summary>
    private void ScheduleDelayedJob(string instanceId, string jobKey, TimeSpan delay)
    {
        var timer = new Timer(
            _ => OnDelayedJobTimerCallback(instanceId, jobKey),
            null,
            delay,
            Timeout.InfiniteTimeSpan); // One-shot timer

        _delayedJobTimers[instanceId] = timer;

        logger.LogDebug(
            "Scheduled delayed job {JobKey}, InstanceId: {InstanceId}, Delay: {Delay}",
            jobKey,
            instanceId,
            delay);
    }

    /// <summary>
    /// Timer callback for delayed job execution.
    /// </summary>
    private async void OnDelayedJobTimerCallback(string instanceId, string jobKey)
    {
        try
        {
            // Remove timer
            if (_delayedJobTimers.TryRemove(instanceId, out var timer))
            {
                timer.Dispose();
            }

            // Transition from Scheduled to Enqueued
            await metadataWriter.UpdateStateAsync(instanceId, JobState.Enqueued);

            logger.LogInformation(
                "Delayed job state transitioned: {JobKey}, InstanceId: {InstanceId}, Scheduled -> Enqueued",
                jobKey,
                instanceId);

            // Publish execution event (unless debug mode)
            if (!_options.TriggeredJobDebugMode)
            {
                var instance = await metadataStore.GetJobInstanceAsync(instanceId);
                if (instance != null)
                {
                    object? parameters = null;
                    if (!string.IsNullOrEmpty(instance.Parameters))
                    {
                        // Parameters are stored as JSON, pass as-is for now
                        // JobExecutor will deserialize based on job type
                        parameters = instance.Parameters;
                    }

                    await PublishJobExecutionEventAsync(instanceId, jobKey, parameters);
                }
            }
            else
            {
                logger.LogDebug(
                    "TriggeredJobDebugMode is enabled. Delayed job {JobKey} instance {InstanceId} transitioned to Enqueued but not published for automatic execution.",
                    jobKey,
                    instanceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in delayed job timer callback for {JobKey}, InstanceId: {InstanceId}: {Message}",
                jobKey,
                instanceId,
                ex.Message);
        }
    }

    /// <summary>
    /// Publishes a job execution event to the event bus.
    /// </summary>
    private async Task PublishJobExecutionEventAsync(
        string instanceId,
        string jobKey,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var executionEvent = new MoJobExecutionEvent
            {
                InstanceId = instanceId,
                JobKey = jobKey,
                Parameters = parameters?.ToString(), // Already JSON string or null
                RequestedAt = DateTime.UtcNow
            };

            await eventBus.PublishAsync(executionEvent);

            logger.LogDebug(
                "Job execution event published: {JobKey}, InstanceId: {InstanceId}",
                jobKey,
                instanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish job execution event for {JobKey}, InstanceId: {InstanceId}: {Message}",
                jobKey,
                instanceId,
                ex.Message);

            // Mark instance as failed
            await metadataWriter.UpdateStateAsync(
                instanceId,
                JobState.Failed,
                $"Event bus publishing failure: {ex.Message}",
                cancellationToken);
        }
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
