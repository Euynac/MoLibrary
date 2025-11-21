using System.Text.Json;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated instance ID (GUID).</returns>
    public async Task<JobInstance> CreateInstanceAsync(
        JobDefinition definition,
        object? parameters,
        JobState initialState,
        CancellationToken cancellationToken = default)
    {
        var instanceId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var instance = new JobInstance
        {
            InstanceId = instanceId,
            JobKey = definition.JobKey,
            State = initialState,
            JobArgs = parameters != null ? JsonSerializer.Serialize(parameters) : null,
            CreatedAt = now,
            RetryAttempt = 0
        };

        await metadataStore.SaveJobInstanceAsync(instance, cancellationToken);

        logger.LogInformation(
            "Created job instance {InstanceId} for job {JobKey} with initial state {InitialState}",
            instanceId,
            definition.JobKey,
            initialState);

        return instance;
    }

    /// <summary>
    /// Updates the state of a job instance with validation of state transitions.
    /// </summary>
    /// <param name="instanceId">The instance ID to update.</param>
    /// <param name="newState">The new state to transition to.</param>
    /// <param name="errorMessage">Optional error message for failed states.</param>
    /// <param name="clientId">The instance ID that triggered this update (must have value when newState is Processing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public async Task UpdateStateAsync(
        string instanceId,
        JobState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default,
        string? clientId = null)
    {
        var instance = await metadataStore.GetJobInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            logger.LogError("Job instance {InstanceId} not found for state update", instanceId);
            throw new InvalidOperationException($"Job instance {instanceId} not found");
        }

        var currentState = instance.State;
        instance.UpdateStateAsync(newState, errorMessage, clientId);
        await metadataStore.SaveJobInstanceAsync(instance, cancellationToken);
        logger.LogInformation(
            "Updated job instance {InstanceId} state from {OldState} to {NewState}",
            instanceId,
            currentState,
            newState);
    }
}
