using System.Collections.Concurrent;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.Events;
using MoLibrary.TaskScheduler.Models;
using MoLibrary.TaskScheduler.Modules;

namespace MoLibrary.TaskScheduler.ControlPlane;

/// <summary>
/// Central orchestrator for task scheduling and execution requests.
/// Implements IHostedService to manage recurring task scheduling via cron expressions
/// and coordinates triggered task execution with optional delays.
/// </summary>
public class MoTaskScheduler(
    IOptions<ModuleTaskSchedulerOption> options,
    IMoTaskScheduleMetadataStore metadataStore,
    TaskRegistry taskRegistry,
    MetadataWriter metadataWriter,
    IMoEventBus eventBus,
    ILogger<MoTaskScheduler> logger) : IHostedService
{
    private readonly ModuleTaskSchedulerOption _options = options.Value;

    // Recurring task scheduling state
    private readonly ConcurrentDictionary<string, RecurringTaskSchedule> _recurringSchedules = new();
    private readonly ConcurrentDictionary<string, Timer> _delayedTaskTimers = new();
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Starts the task scheduler, loading all recurring tasks and scheduling cron-based executions.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = new CancellationTokenSource();

        logger.LogInformation(
            "TaskScheduler starting. RecurringTaskDebugMode: {RecurringDebug}, TriggeredTaskDebugMode: {TriggeredDebug}",
            _options.RecurringTaskDebugMode,
            _options.TriggeredTaskDebugMode);

        // Load all recurring task definitions
        var allDefinitions = await metadataStore.GetAllTaskDefinitionsAsync(cancellationToken);
        var recurringTasks = allDefinitions.Where(d => d.Type == TaskType.Recurring).ToList();

        logger.LogInformation("Loaded {Count} recurring task definitions", recurringTasks.Count);

        // Schedule each recurring task
        foreach (var definition in recurringTasks)
        {
            try
            {
                ScheduleRecurringTask(definition);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to schedule recurring task {TaskKey}: {Message}",
                    definition.TaskKey,
                    ex.Message);
            }
        }

        logger.LogInformation("TaskScheduler started successfully");
    }

    /// <summary>
    /// Stops the task scheduler gracefully, cancelling all pending timers.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TaskScheduler stopping...");

        _stoppingCts?.Cancel();

        // Dispose all recurring task timers
        foreach (var schedule in _recurringSchedules.Values)
        {
            schedule.Timer?.Dispose();
        }
        _recurringSchedules.Clear();

        // Dispose all delayed task timers
        foreach (var timer in _delayedTaskTimers.Values)
        {
            timer?.Dispose();
        }
        _delayedTaskTimers.Clear();

        _stoppingCts?.Dispose();

        logger.LogInformation("TaskScheduler stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a triggered task for execution with optional delay.
    /// </summary>
    /// <typeparam name="TTask">The task type inheriting from TriggeredTask{TParam}.</typeparam>
    /// <param name="parameters">Parameters to pass to the task.</param>
    /// <param name="delay">Optional delay before task execution. If null, task executes immediately.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The instance ID of the created task instance.</returns>
    public async Task<string> EnqueueAsync<TTask>(
        object? parameters = null,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default) where TTask : class
    {
        var taskKey = typeof(TTask).FullName ?? typeof(TTask).Name;

        // Get task definition
        var definition = await taskRegistry.GetDefinitionAsync(taskKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Task {taskKey} is not registered. Ensure the task class inherits from TriggeredTask<TParam> and is discovered during module initialization.");
        }

        // Determine initial state and scheduled time
        TaskState initialState;
        DateTime? scheduledFor = null;

        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            initialState = TaskState.Scheduled;
            scheduledFor = DateTime.UtcNow.Add(delay.Value);
        }
        else
        {
            initialState = TaskState.Enqueued;
        }

        // Create instance
        var instanceId = await metadataWriter.CreateInstanceAsync(
            definition,
            parameters,
            initialState,
            scheduledFor,
            cancellationToken);

        logger.LogInformation(
            "Triggered task enqueued: {TaskKey}, InstanceId: {InstanceId}, Delay: {Delay}, InitialState: {InitialState}",
            taskKey,
            instanceId,
            delay,
            initialState);

        // If delayed, schedule timer for state transition
        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            ScheduleDelayedTask(instanceId, taskKey, delay.Value);
        }
        else if (!_options.TriggeredTaskDebugMode)
        {
            // Immediate execution - publish event (unless debug mode is enabled)
            await PublishTaskExecutionEventAsync(instanceId, taskKey, parameters, cancellationToken);
        }
        else
        {
            logger.LogDebug(
                "TriggeredTaskDebugMode is enabled. Task {TaskKey} instance {InstanceId} created but not published for automatic execution.",
                taskKey,
                instanceId);
        }

        return instanceId;
    }

    /// <summary>
    /// Pauses a recurring task by marking it as disabled in the metadata store.
    /// </summary>
    /// <param name="taskKey">The unique task key to pause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PauseRecurringTaskAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        var definition = await taskRegistry.GetDefinitionAsync(taskKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Task {taskKey} not found");
        }

        if (definition.Type != TaskType.Recurring)
        {
            throw new InvalidOperationException($"Task {taskKey} is not a recurring task");
        }

        // Update definition in metadata store
        definition.IsDisabled = true;
        await metadataStore.SaveTaskDefinitionAsync(definition, cancellationToken);

        // Cancel timer if exists
        if (_recurringSchedules.TryRemove(taskKey, out var schedule))
        {
            schedule.Timer?.Dispose();
        }

        logger.LogInformation("Recurring task paused: {TaskKey}", taskKey);
    }

    /// <summary>
    /// Resumes a paused recurring task by marking it as enabled and rescheduling.
    /// </summary>
    /// <param name="taskKey">The unique task key to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ResumeRecurringTaskAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        var definition = await taskRegistry.GetDefinitionAsync(taskKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Task {taskKey} not found");
        }

        if (definition.Type != TaskType.Recurring)
        {
            throw new InvalidOperationException($"Task {taskKey} is not a recurring task");
        }

        // Update definition in metadata store
        definition.IsDisabled = false;
        await metadataStore.SaveTaskDefinitionAsync(definition, cancellationToken);

        // Reschedule
        ScheduleRecurringTask(definition);

        logger.LogInformation("Recurring task resumed: {TaskKey}", taskKey);
    }

    /// <summary>
    /// Schedules a recurring task using its cron expression.
    /// </summary>
    private void ScheduleRecurringTask(TaskDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.CronExpression))
        {
            logger.LogDebug(
                "Recurring task {TaskKey} has no cron expression. Skipping automatic scheduling.",
                definition.TaskKey);
            return;
        }

        if (definition.IsDisabled)
        {
            logger.LogDebug(
                "Recurring task {TaskKey} is disabled. Skipping scheduling.",
                definition.TaskKey);
            return;
        }

        if (_options.RecurringTaskDebugMode)
        {
            logger.LogDebug(
                "RecurringTaskDebugMode is enabled. Task {TaskKey} will not be automatically scheduled.",
                definition.TaskKey);
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
                    "Cron expression for task {TaskKey} has no future occurrences: {CronExpression}",
                    definition.TaskKey,
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
                _ => OnRecurringTaskTimerCallback(definition.TaskKey),
                null,
                dueTime,
                Timeout.InfiniteTimeSpan); // One-shot timer

            var schedule = new RecurringTaskSchedule
            {
                TaskKey = definition.TaskKey,
                CronExpression = cronExpression,
                Timer = timer,
                NextOccurrence = nextOccurrence.Value
            };

            _recurringSchedules[definition.TaskKey] = schedule;

            logger.LogDebug(
                "Scheduled recurring task {TaskKey}, next execution at {NextExecution} (in {DueTime})",
                definition.TaskKey,
                nextOccurrence.Value,
                dueTime);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to parse cron expression for task {TaskKey}: {CronExpression}",
                definition.TaskKey,
                definition.CronExpression);
        }
    }

    /// <summary>
    /// Timer callback for recurring task execution.
    /// </summary>
    private async void OnRecurringTaskTimerCallback(string taskKey)
    {
        try
        {
            // Get current task definition (may have been updated)
            var definition = await taskRegistry.GetDefinitionAsync(taskKey);
            if (definition == null)
            {
                logger.LogWarning(
                    "Recurring task {TaskKey} not found during timer callback. Removing from schedule.",
                    taskKey);
                _recurringSchedules.TryRemove(taskKey, out _);
                return;
            }

            // Check if task is still enabled
            if (definition.IsDisabled)
            {
                logger.LogDebug(
                    "Recurring task {TaskKey} is disabled. Skipping execution.",
                    taskKey);
                _recurringSchedules.TryRemove(taskKey, out var oldSchedule);
                oldSchedule?.Timer?.Dispose();
                return;
            }

            var now = DateTime.UtcNow;

            // Check start/end time constraints
            if (definition.StartTime.HasValue && now < definition.StartTime.Value)
            {
                logger.LogDebug(
                    "Recurring task {TaskKey} execution skipped (before start time {StartTime})",
                    taskKey,
                    definition.StartTime.Value);

                // Reschedule for next occurrence
                ScheduleRecurringTask(definition);
                return;
            }

            if (definition.EndTime.HasValue && now > definition.EndTime.Value)
            {
                logger.LogInformation(
                    "Recurring task {TaskKey} has passed end time {EndTime}. Removing from schedule.",
                    taskKey,
                    definition.EndTime.Value);

                _recurringSchedules.TryRemove(taskKey, out var oldSchedule);
                oldSchedule?.Timer?.Dispose();
                return;
            }

            // Create instance and publish event
            var instanceId = await metadataWriter.CreateInstanceAsync(
                definition,
                parameters: null,
                TaskState.Enqueued,
                scheduledFor: null);

            logger.LogInformation(
                "Recurring task triggered: {TaskKey}, InstanceId: {InstanceId}",
                taskKey,
                instanceId);

            await PublishTaskExecutionEventAsync(instanceId, taskKey, null);

            // Reschedule for next occurrence
            ScheduleRecurringTask(definition);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in recurring task timer callback for {TaskKey}: {Message}",
                taskKey,
                ex.Message);

            // Try to reschedule despite error
            try
            {
                var definition = await taskRegistry.GetDefinitionAsync(taskKey);
                if (definition != null)
                {
                    ScheduleRecurringTask(definition);
                }
            }
            catch (Exception rescheduleEx)
            {
                logger.LogError(
                    rescheduleEx,
                    "Failed to reschedule recurring task {TaskKey} after error: {Message}",
                    taskKey,
                    rescheduleEx.Message);
            }
        }
    }

    /// <summary>
    /// Schedules a delayed task timer.
    /// </summary>
    private void ScheduleDelayedTask(string instanceId, string taskKey, TimeSpan delay)
    {
        var timer = new Timer(
            _ => OnDelayedTaskTimerCallback(instanceId, taskKey),
            null,
            delay,
            Timeout.InfiniteTimeSpan); // One-shot timer

        _delayedTaskTimers[instanceId] = timer;

        logger.LogDebug(
            "Scheduled delayed task {TaskKey}, InstanceId: {InstanceId}, Delay: {Delay}",
            taskKey,
            instanceId,
            delay);
    }

    /// <summary>
    /// Timer callback for delayed task execution.
    /// </summary>
    private async void OnDelayedTaskTimerCallback(string instanceId, string taskKey)
    {
        try
        {
            // Remove timer
            if (_delayedTaskTimers.TryRemove(instanceId, out var timer))
            {
                timer.Dispose();
            }

            // Transition from Scheduled to Enqueued
            await metadataWriter.UpdateStateAsync(instanceId, TaskState.Enqueued);

            logger.LogInformation(
                "Delayed task state transitioned: {TaskKey}, InstanceId: {InstanceId}, Scheduled -> Enqueued",
                taskKey,
                instanceId);

            // Publish execution event (unless debug mode)
            if (!_options.TriggeredTaskDebugMode)
            {
                var instance = await metadataStore.GetTaskInstanceAsync(instanceId);
                if (instance != null)
                {
                    object? parameters = null;
                    if (!string.IsNullOrEmpty(instance.Parameters))
                    {
                        // Parameters are stored as JSON, pass as-is for now
                        // TaskExecutor will deserialize based on task type
                        parameters = instance.Parameters;
                    }

                    await PublishTaskExecutionEventAsync(instanceId, taskKey, parameters);
                }
            }
            else
            {
                logger.LogDebug(
                    "TriggeredTaskDebugMode is enabled. Delayed task {TaskKey} instance {InstanceId} transitioned to Enqueued but not published for automatic execution.",
                    taskKey,
                    instanceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in delayed task timer callback for {TaskKey}, InstanceId: {InstanceId}: {Message}",
                taskKey,
                instanceId,
                ex.Message);
        }
    }

    /// <summary>
    /// Publishes a task execution event to the event bus.
    /// </summary>
    private async Task PublishTaskExecutionEventAsync(
        string instanceId,
        string taskKey,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var executionEvent = new TaskExecutionEvent
            {
                InstanceId = instanceId,
                TaskKey = taskKey,
                Parameters = parameters?.ToString(), // Already JSON string or null
                RequestedAt = DateTime.UtcNow
            };

            await eventBus.PublishAsync(executionEvent);

            logger.LogDebug(
                "Task execution event published: {TaskKey}, InstanceId: {InstanceId}",
                taskKey,
                instanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish task execution event for {TaskKey}, InstanceId: {InstanceId}: {Message}",
                taskKey,
                instanceId,
                ex.Message);

            // Mark instance as failed
            await metadataWriter.UpdateStateAsync(
                instanceId,
                TaskState.Failed,
                $"Event bus publishing failure: {ex.Message}",
                cancellationToken);
        }
    }

    /// <summary>
    /// Internal class to track recurring task scheduling state.
    /// </summary>
    private class RecurringTaskSchedule
    {
        public required string TaskKey { get; init; }
        public required CronExpression CronExpression { get; init; }
        public Timer? Timer { get; set; }
        public DateTime NextOccurrence { get; set; }
    }
}
