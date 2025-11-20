using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Job dispatcher is responsible for publishing job execution events to the event bus.
/// </summary>
public class JobDispatcher(IMoEventBus eventBus, ILogger<JobDispatcher> logger, JobInstanceManager jobInstanceManager)
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
            var executionEvent = new MoJobExecutionEvent
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