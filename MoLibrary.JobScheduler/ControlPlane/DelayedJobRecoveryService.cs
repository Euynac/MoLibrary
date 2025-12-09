using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Handles recovery of scheduled jobs on service restart.
/// Loads jobs in Scheduled state and reschedules them.
/// </summary>
public class DelayedJobRecoveryService(
    IMoJobMetadataRepository metadataRepository,
    IJobDefinitionCacheService cacheService,
    JobInstanceManager jobInstanceManager,
    ILogger<DelayedJobRecoveryService> logger)
{
    /// <summary>
    /// Recovers scheduled jobs from metadata store.
    /// Validates definitions, handles disabled/missing jobs, and reschedules valid ones.
    /// </summary>
    /// <param name="scheduleDelayedJobCallback">Callback to schedule recovered jobs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RecoverScheduledJobsAsync(
        Func<JobInstance, JobDefinition, DateTime, Task> scheduleDelayedJobCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Recovering scheduled jobs...");

            var query = new JobInstanceQuery
            {
                State = JobState.Scheduled,
                PageNumber = 1,
                PageSize = 10000
            };
            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var scheduledInstances = result.Items;

            if (scheduledInstances.Count == 0)
            {
                logger.LogInformation("No scheduled jobs to recover");
                return;
            }

            logger.LogInformation("Recovering {Count} scheduled job(s)", scheduledInstances.Count);

            foreach (var instance in scheduledInstances)
            {
                if (!instance.ScheduledExecutionTime.HasValue)
                {
                    logger.LogWarning(
                        "Instance {InstanceId} missing ScheduledExecutionTime, marking as Failed",
                        instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Failed,
                        "Missing ScheduledExecutionTime during recovery",
                        cancellationToken);
                    continue;
                }

                var definition = await cacheService.GetDefinitionAsync(instance.JobKey, cancellationToken);
                if (definition == null)
                {
                    logger.LogWarning("Job definition not found for {InstanceId}", instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Failed,
                        "Job definition not found during recovery",
                        cancellationToken);
                    continue;
                }

                if (definition.IsDisabled)
                {
                    logger.LogInformation("Job disabled, cancelling {InstanceId}", instance.InstanceId);
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Cancelled,
                        "Job disabled during recovery",
                        cancellationToken);
                    continue;
                }

                await scheduleDelayedJobCallback(instance, definition, instance.ScheduledExecutionTime.Value);
                logger.LogInformation("Recovered {InstanceId}, scheduled for {Time}",
                    instance.InstanceId,
                    instance.ScheduledExecutionTime.Value);
            }

            logger.LogInformation("Scheduled job recovery completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recovering scheduled jobs");
        }
    }
}
