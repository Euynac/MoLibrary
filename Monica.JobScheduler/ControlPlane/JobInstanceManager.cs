using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.Modules;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Handles job instance creation and state transitions with validation.
/// Ensures state transitions follow the defined state machine and manages instance timestamps.
/// Publishes lifecycle events for state changes.
/// </summary>
public class JobInstanceManager(
    IMoJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))]IMoEventBus eventBus,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobInstanceManager> logger)
{
    /// <summary>
    /// Creates a new job instance with the specified initial state.
    /// </summary>
    /// <param name="definition">The job definition for this instance.</param>
    /// <param name="parameters">Optional parameters for triggered jobs (will be JSON-serialized).</param>
    /// <param name="initialState">The initial state for the job instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="instanceId">Optional pre-generated instance ID. If null, a new GUID will be generated.</param>
    /// <param name="initDescription">Optional description for initial state history entry.</param>
    /// <returns>The generated instance ID (GUID).</returns>
    public async Task<JobInstance> CreateInstanceAsync(
        JobDefinition definition,
        object? parameters,
        JobState initialState,
        CancellationToken cancellationToken = default,
        string? instanceId = null,
        string? initDescription = null)
    {
        instanceId ??= Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var instance = new JobInstance
        {
            InstanceId = instanceId,
            JobKey = definition.JobKey,
            JobArgs = parameters != null ? JsonSerializer.Serialize(parameters, options.Value.JobArgsSerializerOptions) : null,
            CreatedAt = now,
            State = initialState,
            RetryAttempt = 0
        };
        instance.AppendStateHistory(
            JobInstance.CreatedStateHistoryLabel,
            initialState,
            initDescription ?? "Instance created",
            now);

        await metadataRepository.SaveInstanceAsync(instance, cancellationToken);

        logger.LogDebug(
            "Created job instance {InstanceId} for job {JobKey} with initial state {InitialState}",
            instanceId,
            definition.JobKey,
            initialState);

        return instance;
    }

    /// <summary>
    /// Updates the state of a job instance with validation of state transitions.
    /// Publishes lifecycle events: JobStartedEvent when transitioning to Processing,
    /// JobCompletedEvent when transitioning to terminal states.
    /// </summary>
    /// <param name="instanceId">The instance ID to update.</param>
    /// <param name="newState">The new state to transition to.</param>
    /// <param name="message">Optional message to record with state transition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="clientId">The worker instance ID (required when newState is Processing).</param>
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public async Task UpdateStateAsync(
        string instanceId,
        JobState newState,
        string? message = null,
        CancellationToken cancellationToken = default,
        string? clientId = null)
    {
        var instance = await metadataRepository.GetInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            logger.LogError("Job instance {InstanceId} not found for state update", instanceId);
            throw new InvalidOperationException($"Job instance {instanceId} not found");
        }

        var currentState = instance.State;
        instance.UpdateStateAsync(newState, message, clientId);
        await metadataRepository.SaveInstanceAsync(instance, cancellationToken);

        logger.LogDebug(
            "Updated job instance {InstanceId} state from {OldState} to {NewState}",
            instanceId,
            currentState,
            newState);

        // Publish lifecycle events based on state transitions
        await PublishLifecycleEventAsync(instance, currentState, newState);
    }

    /// <summary>
    /// Publishes appropriate lifecycle events based on the new state.
    /// </summary>
    private async Task PublishLifecycleEventAsync(JobInstance instance, JobState oldState, JobState newState)
    {
        try
        {
            // Publish JobStartedEvent when job starts processing
            if (newState == JobState.Processing)
            {
                if (string.IsNullOrEmpty(instance.RunningClientId))
                {
                    logger.LogWarning(
                        "Job instance {InstanceId} entered Processing state without RunningClientId",
                        instance.InstanceId);
                    return;
                }

                await eventBus.PublishAsync(new JobStartedEvent
                {
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    WorkerClientId = instance.RunningClientId,
                    StartedAt = instance.StartedAt ?? DateTime.UtcNow
                });

                logger.LogDebug(
                    "Published JobStartedEvent for instance {InstanceId}",
                    instance.InstanceId);
            }
            // Publish JobCompletedEvent when job reaches a terminal state from processing.
            else if (IsTerminalState(newState))
            {
                if (string.IsNullOrEmpty(instance.RunningClientId))
                {
                    logger.LogWarning(
                        "Job instance {InstanceId} from {OldState} reached terminal state {State} without RunningClientId",
                        instance.InstanceId,
                        oldState,
                        newState);
                    return;
                }

                await eventBus.PublishAsync(new JobCompletedEvent
                {
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    WorkerClientId = instance.RunningClientId,
                    FinalState = newState,
                    CompletedAt = instance.CompletedAt ?? DateTime.UtcNow
                });

                logger.LogDebug(
                    "Published JobCompletedEvent for instance {InstanceId} from {OldState} to {State}",
                    instance.InstanceId,
                    oldState,
                    newState);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - event publishing failure should not break state updates
            logger.LogError(
                ex,
                "Failed to publish lifecycle event for instance {InstanceId} with state {State}",
                instance.InstanceId,
                newState);
        }
    }

    /// <summary>
    /// Determines if a state is terminal (job execution has finished).
    /// </summary>
    private static bool IsTerminalState(JobState state) => state is
        JobState.Succeeded or
        JobState.Failed or
        JobState.Terminated or
        JobState.Cancelled or
        JobState.Skipped;
}
