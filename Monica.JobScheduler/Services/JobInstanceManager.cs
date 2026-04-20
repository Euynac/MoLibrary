using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Utils;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Handles job instance creation and state transitions with validation.
/// Ensures state transitions follow the defined state machine and manages instance timestamps.
/// Publishes lifecycle events for state changes.
/// </summary>
public class JobInstanceManager(
    IJobMetadataRepository metadataRepository,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IEventBus eventBus,
    IServiceDiscoveryClientInfo clientInfo,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobInstanceManager> logger)
{
    private readonly ModuleJobSchedulerOption _jobSchedulerOptions = options.Value;

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
            SchedulerScopeKey = definition.SchedulerScopeKey,
            InstanceId = instanceId,
            JobKey = definition.JobKey,
            JobArgs = parameters != null ? JsonSerializer.Serialize(parameters, _jobSchedulerOptions.JobArgsSerializerOptions) : null,
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
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public async Task UpdateStateAsync(
        string instanceId,
        JobState newState,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var mutation = await MutateInstanceAsync(
            instanceId,
            instance =>
            {
                var currentState = instance.State;
                var sourceClientId = ResolveSourceClientId(newState);

                instance.UpdateStateAsync(newState, message, sourceClientId);
                return new InstanceMutationResult(instance, currentState, newState, sourceClientId);
            },
            cancellationToken);

        logger.LogDebug(
            "Updated job instance {InstanceId} state from {OldState} to {NewState} by client {SourceClientId}",
            instanceId,
            mutation.OldState,
            newState,
            mutation.SourceClientId ?? "(unknown)");

        await PublishLifecycleEventAsync(mutation.Instance, mutation.OldState!.Value, mutation.NewState!.Value);
    }

    /// <summary>
    /// Appends an execution log entry to an existing job instance without changing its state.
    /// </summary>
    internal async Task AppendExecutionLogAsync(
        string instanceId,
        string message,
        LogLevel logLevel = LogLevel.Information,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));
        }

        if (string.IsNullOrWhiteSpace(message) && exception == null)
        {
            throw new ArgumentException("Either a message or an exception must be provided.", nameof(message));
        }

        await MutateInstanceAsync(
            instanceId,
            instance =>
            {
                instance.AppendExecutionLog(
                    message,
                    logLevel,
                    exception,
                    DateTime.UtcNow,
                    TryResolveSourceClientId());

                return new InstanceMutationResult(instance);
            },
            cancellationToken);

        logger.LogDebug(
            "Appended execution log to job instance {InstanceId} with level {LogLevel}",
            instanceId,
            logLevel);
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
                    SchedulerScopeKey = instance.SchedulerScopeKey,
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    WorkerClientId = instance.RunningClientId,
                    StartedAt = instance.StartedAt ?? DateTime.UtcNow
                }, JobEventTopicHelper.GetTopicName<JobStartedEvent>(_jobSchedulerOptions.SchedulerScopeKey));

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
                    SchedulerScopeKey = instance.SchedulerScopeKey,
                    InstanceId = instance.InstanceId,
                    JobKey = instance.JobKey,
                    WorkerClientId = instance.RunningClientId,
                    FinalState = newState,
                    CompletedAt = instance.CompletedAt ?? DateTime.UtcNow
                }, JobEventTopicHelper.GetTopicName<JobCompletedEvent>(_jobSchedulerOptions.SchedulerScopeKey));

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

    private string? ResolveSourceClientId(JobState newState)
    {
        var sourceClientId = TryResolveSourceClientId();
        if (!string.IsNullOrWhiteSpace(sourceClientId))
        {
            return sourceClientId;
        }

        if (newState == JobState.Processing)
        {
            throw new InvalidOperationException("RunningClientId must be available when transitioning to Processing.");
        }

        return null;
    }

    private string? TryResolveSourceClientId()
    {
        try
        {
            var sourceClientId = clientInfo.GetServiceStatus().InstanceId;
            if (!string.IsNullOrWhiteSpace(sourceClientId))
            {
                return sourceClientId;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve current source client id for job instance mutation");
        }

        return null;
    }

    private async Task<InstanceMutationResult> MutateInstanceAsync(
        string instanceId,
        Func<JobInstance, InstanceMutationResult> mutate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));
        }

        // TODO: This read-mutate-save flow can race when multiple local or distributed actors mutate
        // the same instance. Revisit with a repository-level atomic mutation/concurrency design.
        var instance = await metadataRepository.GetInstanceAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            logger.LogError("Job instance {InstanceId} not found for mutation", instanceId);
            throw new InvalidOperationException($"Job instance {instanceId} not found");
        }

        var mutation = mutate(instance);
        await metadataRepository.SaveInstanceAsync(instance, cancellationToken);
        return mutation;
    }

    private sealed record InstanceMutationResult(
        JobInstance Instance,
        JobState? OldState = null,
        JobState? NewState = null,
        string? SourceClientId = null);
}
