using System.Text.Json;
using Microsoft.Extensions.Logging;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.ControlPlane;

/// <summary>
/// Handles task instance creation and state transitions with validation.
/// Ensures state transitions follow the defined state machine and manages instance timestamps.
/// </summary>
public class MetadataWriter(
    IMoTaskScheduleMetadataStore metadataStore,
    ILogger<MetadataWriter> logger)
{
    /// <summary>
    /// Creates a new task instance with the specified initial state.
    /// </summary>
    /// <param name="definition">The task definition for this instance.</param>
    /// <param name="parameters">Optional parameters for triggered tasks (will be JSON-serialized).</param>
    /// <param name="initialState">The initial state for the task instance.</param>
    /// <param name="scheduledFor">Optional scheduled execution time for Scheduled state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated instance ID (GUID).</returns>
    public async Task<string> CreateInstanceAsync(
        TaskDefinition definition,
        object? parameters,
        TaskState initialState,
        DateTime? scheduledFor = null,
        CancellationToken cancellationToken = default)
    {
        var instanceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var instance = new TaskInstance
        {
            InstanceId = instanceId,
            TaskKey = definition.TaskKey,
            State = initialState,
            Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
            CreatedAt = now,
            ScheduledFor = initialState == TaskState.Scheduled ? scheduledFor : null,
            RetryAttempt = 0
        };

        await metadataStore.SaveTaskInstanceAsync(instance, cancellationToken);

        logger.LogInformation(
            "Created task instance {InstanceId} for task {TaskKey} with initial state {InitialState}",
            instanceId,
            definition.TaskKey,
            initialState);

        return instanceId;
    }

    /// <summary>
    /// Updates the state of a task instance with validation of state transitions.
    /// </summary>
    /// <param name="instanceId">The instance ID to update.</param>
    /// <param name="newState">The new state to transition to.</param>
    /// <param name="errorMessage">Optional error message for failed states.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public async Task UpdateStateAsync(
        string instanceId,
        TaskState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await metadataStore.GetTaskInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            logger.LogError("Task instance {InstanceId} not found for state update", instanceId);
            throw new InvalidOperationException($"Task instance {instanceId} not found");
        }

        var currentState = instance.State;

        // Validate state transition
        if (!IsValidTransition(currentState, newState))
        {
            var message = $"Invalid state transition from {currentState} to {newState} for instance {instanceId}";
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        // Update state
        instance.State = newState;

        // Update timestamps based on new state
        var now = DateTime.UtcNow;
        switch (newState)
        {
            case TaskState.Processing:
                instance.StartedAt = now;
                break;

            case TaskState.Succeeded:
            case TaskState.Failed:
            case TaskState.Terminated:
            case TaskState.Cancelled:
            case TaskState.Skipped:
                // Terminal states (and Failed which may retry but we still timestamp it)
                instance.CompletedAt = now;
                break;
        }

        // Set error message if provided
        if (!string.IsNullOrEmpty(errorMessage))
        {
            instance.ErrorMessage = errorMessage;
        }

        // Save updated instance
        await metadataStore.UpdateTaskStateAsync(
            instanceId,
            newState,
            errorMessage,
            cancellationToken);

        logger.LogInformation(
            "Updated task instance {InstanceId} state from {OldState} to {NewState}",
            instanceId,
            currentState,
            newState);
    }

    /// <summary>
    /// Validates whether a state transition is allowed according to the state machine.
    /// </summary>
    /// <param name="currentState">The current state.</param>
    /// <param name="newState">The desired new state.</param>
    /// <returns>True if the transition is valid, false otherwise.</returns>
    private static bool IsValidTransition(TaskState currentState, TaskState newState)
    {
        // Allow transition to same state (idempotent updates)
        if (currentState == newState)
        {
            return true;
        }

        return currentState switch
        {
            // Scheduled can transition to Enqueued or Cancelled
            TaskState.Scheduled => newState is TaskState.Enqueued or TaskState.Cancelled,

            // Enqueued can transition to Processing, Skipped, or Cancelled
            TaskState.Enqueued => newState is TaskState.Processing or TaskState.Skipped or TaskState.Cancelled,

            // Processing can transition to Succeeded, Failed, or Cancelled
            TaskState.Processing => newState is TaskState.Succeeded or TaskState.Failed or TaskState.Cancelled,

            // Failed can transition to Processing (retry) or Terminated (no retries)
            TaskState.Failed => newState is TaskState.Processing or TaskState.Terminated,

            // Terminal states cannot transition to any other state
            TaskState.Succeeded => false,
            TaskState.Terminated => false,
            TaskState.Cancelled => false,
            TaskState.Skipped => false,

            _ => false
        };
    }
}
