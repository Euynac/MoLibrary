using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Helpers;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Modules;

namespace Monica.JobScheduler.ControlPlane;

/// <summary>
/// Job dispatcher is responsible for publishing job execution events to the event bus.
/// </summary>
public class JobDispatcher(
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    ILogger<JobDispatcher> logger,
    JobInstanceManager jobInstanceManager,
    IJobConcurrencyGuard concurrencyGuard)
{
    /// <summary>
    /// Publishes a job execution event to the event bus.
    /// Atomically reserves a slot before publishing to prevent race conditions.
    /// </summary>
    /// <param name="instance">The job instance to publish.</param>
    /// <param name="definition">The job definition.</param>
    /// <param name="jobArgsJson">Pre-serialized JSON string of job arguments, or null for jobs without arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishJobExecutionEventAsync(
        JobInstance instance,
        JobDefinition definition,
        string? jobArgsJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Atomically reserve execution slot (with local lock)
            var reservationResult = await concurrencyGuard.TryReserveExecutionSlotAsync(
                definition.JobKey,
                instance.InstanceId,
                cancellationToken);

            if (!reservationResult.Reserved)
            {
                logger.LogDebug(
                    "Job {JobKey} instance {InstanceId} could not reserve execution slot: {Reason}",
                    definition.JobKey,
                    instance.InstanceId,
                    reservationResult.Reason);

                // Mark instance as Skipped with specific reason
                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Skipped,
                    reservationResult.Reason!,
                    cancellationToken);

                return;
            }

            // Publish event (slot is reserved)
            var executionEvent = new JobExecutionEvent
            {
                InstanceId = instance.InstanceId,
                JobKey = definition.JobKey,
                JobArgs = jobArgsJson,
                JobArgsKey = definition.JobArgsKey,
                RequestedAt = DateTime.UtcNow,
                MaxExecutionTimeout = definition.MaxExecutionTimeout,
                JobType = definition.JobType,
            };

            var topicName = JobEventTopicHelper.GetTopicName<JobExecutionEvent>(definition.FromProject);
            await eventBus.PublishAsync(executionEvent, topicName, cancellationToken);

            logger.LogDebug(
                "Job execution event published to topic {TopicName}: {JobKey}, InstanceId: {InstanceId}",
                topicName,
                definition.JobKey,
                instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish job execution event for {JobKey}, InstanceId: {InstanceId}: {Message}",
                definition.JobKey,
                instance.InstanceId,
                ex.Message);

            // Release reservation if we reserved but failed to publish
            try
            {
                await concurrencyGuard.ReleaseReservedSlotAsync(
                    definition.JobKey,
                    instance.InstanceId,
                    cancellationToken);
            }
            catch (Exception releaseEx)
            {
                logger.LogWarning(
                    releaseEx,
                    "Failed to release reservation after publish failure for instance {InstanceId}",
                    instance.InstanceId);
            }

            // Mark instance as failed
            await jobInstanceManager.UpdateStateAsync(
                instance.InstanceId,
                JobState.Failed,
                $"Event bus publishing failure: {ex}",
                cancellationToken);
        }
    }

}