using System.Text.Json;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Metadata;

/// <summary>
/// Handles job instance creation and state transitions with validation.
/// Ensures state transitions follow the defined state machine and manages instance timestamps.
/// </summary>
public class JobInstanceManager(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobInstanceManager> logger)
{
    /// <summary>
    /// Creates a new job instance with the specified initial state.
    /// </summary>
    /// <param name="definition">The job definition for this instance.</param>
    /// <param name="parameters">Optional parameters for triggered jobs (will be JSON-serialized).</param>
    /// <param name="initialState">The initial state for the job instance.</param>
    /// <param name="scheduledFor">Optional scheduled execution time for Scheduled state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated instance ID (GUID).</returns>
    public async Task<string> CreateInstanceAsync(
        JobDefinition definition,
        object? parameters,
        JobState initialState,
        DateTime? scheduledFor = null,
        CancellationToken cancellationToken = default)
    {
        var instanceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var instance = new JobInstance
        {
            InstanceId = instanceId,
            JobKey = definition.JobKey,
            State = initialState,
            Parameters = parameters != null ? JsonSerializer.Serialize(parameters) : null,
            CreatedAt = now,
            ScheduledFor = initialState == JobState.Scheduled ? scheduledFor : null,
            RetryAttempt = 0
        };

        await metadataStore.SaveJobInstanceAsync(instance, cancellationToken);

        logger.LogInformation(
            "Created job instance {InstanceId} for job {JobKey} with initial state {InitialState}",
            instanceId,
            definition.JobKey,
            initialState);

        return instanceId;
    }

    /// <summary>
    /// Updates the state of a job instance with validation of state transitions.
    /// </summary>
    /// <param name="instanceId">The instance ID to update.</param>
    /// <param name="newState">The new state to transition to.</param>
    /// <param name="errorMessage">Optional error message for failed states.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public async Task UpdateStateAsync(
        string instanceId,
        JobState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await metadataStore.GetJobInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            logger.LogError("Job instance {InstanceId} not found for state update", instanceId);
            throw new InvalidOperationException($"Job instance {instanceId} not found");
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
            case JobState.Processing:
                instance.StartedAt = now;
                break;

            case JobState.Succeeded:
            case JobState.Failed:
            case JobState.Terminated:
            case JobState.Cancelled:
            case JobState.Skipped:
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
        await metadataStore.UpdateJobStateAsync(
            instanceId,
            newState,
            errorMessage,
            cancellationToken);

        logger.LogInformation(
            "Updated job instance {InstanceId} state from {OldState} to {NewState}",
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
    private static bool IsValidTransition(JobState currentState, JobState newState)
    {
        // Allow transition to same state (idempotent updates)
        if (currentState == newState)
        {
            return true;
        }

        return currentState switch
        {
            // Scheduled can transition to Enqueued or Cancelled
            JobState.Scheduled => newState is JobState.Enqueued or JobState.Cancelled,

            // Enqueued can transition to Processing, Skipped, or Cancelled
            JobState.Enqueued => newState is JobState.Processing or JobState.Skipped or JobState.Cancelled,

            // Processing can transition to Succeeded, Failed, or Cancelled
            JobState.Processing => newState is JobState.Succeeded or JobState.Failed or JobState.Cancelled,

            // Failed can transition to Processing (retry) or Terminated (no retries)
            JobState.Failed => newState is JobState.Processing or JobState.Terminated,

            // Terminal states cannot transition to any other state
            JobState.Succeeded => false,
            JobState.Terminated => false,
            JobState.Cancelled => false,
            JobState.Skipped => false,

            _ => false
        };
    }
}
