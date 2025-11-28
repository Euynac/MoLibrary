using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;

namespace MoLibrary.JobScheduler.ControlPlane;

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
    public async Task PublishJobExecutionEventAsync(
        JobInstance instance,
        JobDefinition definition,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Atomically reserve execution slot (with local lock)
            var reserved = await concurrencyGuard.TryReserveExecutionSlotAsync(
                definition.JobKey,
                instance.InstanceId,
                cancellationToken);

            if (!reserved)
            {
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} could not reserve execution slot (MaxConcurrency: {MaxConcurrency})",
                    definition.JobKey,
                    instance.InstanceId,
                    definition.MaxConcurrency);

                // Mark instance as Skipped
                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Skipped,
                    $"Could not reserve execution slot (MaxConcurrency: {definition.MaxConcurrency})",
                    cancellationToken);

                return;
            }

            // Publish event (slot is reserved)
            var executionEvent = new JobExecutionEvent
            {
                InstanceId = instance.InstanceId,
                JobKey = definition.JobKey,
                JobArgs = parameters?.ToString(), // Already JSON string or null
                RequestedAt = DateTime.UtcNow,
                MaxExecutionTimeout = definition.MaxExecutionTimeout,
                JobType = definition.JobType,
            };

            await eventBus.PublishAsync(executionEvent);

            logger.LogDebug(
                "Job execution event published: {JobKey}, InstanceId: {InstanceId}",
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
                $"Event bus publishing failure: {ex.Message}",
                cancellationToken);
        }
    }

}