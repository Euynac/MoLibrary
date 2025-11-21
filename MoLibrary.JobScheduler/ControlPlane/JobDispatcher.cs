using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Job dispatcher is responsible for publishing job execution events to the event bus.
/// </summary>
public class JobDispatcher(
    IMoEventBus eventBus,
    ILogger<JobDispatcher> logger,
    JobInstanceManager jobInstanceManager,
    IJobConcurrencyGuard concurrencyGuard)
{
    /// <summary>
    /// Publishes a job execution event to the event bus.
    /// </summary>
    public async Task PublishJobExecutionEventAsync(
        JobInstance instance,
        JobDefinition definition,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check concurrency limit before publishing
            var canExecute = await concurrencyGuard.CanExecuteJobAsync(definition.JobKey, cancellationToken);
            if (!canExecute)
            {
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} skipped due to MaxConcurrency limit ({MaxConcurrency})",
                    definition.JobKey,
                    instance.InstanceId,
                    definition.MaxConcurrency);

                // Mark instance as Skipped
                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Skipped,
                    $"Exceeded MaxConcurrency limit of {definition.MaxConcurrency}",
                    cancellationToken);

                return;
            }

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
                definition,
                instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish job execution event for {JobKey}, InstanceId: {InstanceId}: {Message}",
                definition,
                instance.InstanceId,
                ex.Message);

            // Mark instance as failed
            await jobInstanceManager.UpdateStateAsync(
                instance.InstanceId,
                JobState.Failed,
                $"Event bus publishing failure: {ex.Message}",
                cancellationToken);
        }
    }

}