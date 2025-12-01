using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Validates recurring job definitions for scheduling.
/// Checks job existence, disabled state, cron expressions, and time constraints.
/// </summary>
public class RecurringJobValidator(
    IJobDefinitionCacheService cacheService,
    ILogger<RecurringJobValidator> logger)
{
    /// <summary>
    /// Gets and validates a recurring job definition for scheduling.
    /// Returns null if job is invalid, missing, disabled, or outside time constraints.
    /// </summary>
    /// <param name="jobKey">Job key to validate</param>
    /// <param name="onInvalidScheduleRemoval">Callback to remove schedule when validation fails</param>
    /// <param name="onBeforeStartTime">Callback when job is before start time (for rescheduling)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Valid job definition or null</returns>
    public async Task<JobDefinition?> GetValidatedRecurringJobAsync(
        string jobKey,
        Func<string, Task> onInvalidScheduleRemoval,
        Func<JobDefinition, Task>? onBeforeStartTime = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await cacheService.GetJobDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            logger.LogWarning(
                "Recurring job {JobKey} not found during validation. Removing from schedule.",
                jobKey);
            await onInvalidScheduleRemoval(jobKey);
            return null;
        }

        if (string.IsNullOrWhiteSpace(definition.CronExpression))
        {
            logger.LogWarning(
                "Recurring job {JobKey} has no cron expression. Skipping automatic scheduling.",
                definition.JobKey);
            await onInvalidScheduleRemoval(jobKey);
            return null;
        }

        if (definition.IsDisabled)
        {
            logger.LogWarning(
                "Recurring job {JobKey} is disabled. Skipping scheduling.",
                definition.JobKey);
            await onInvalidScheduleRemoval(jobKey);
            return null;
        }

        var now = DateTime.UtcNow;

        // Check start/end time constraints
        if (definition.StartTime.HasValue && now < definition.StartTime.Value)
        {
            logger.LogDebug(
                "Recurring job {JobKey} schedule skipped (before start time {StartTime})",
                jobKey,
                definition.StartTime.Value);

            // Notify caller for rescheduling if callback provided
            if (onBeforeStartTime != null)
            {
                await onBeforeStartTime(definition);
            }

            await onInvalidScheduleRemoval(jobKey);
            return null;
        }

        if (definition.EndTime.HasValue && now > definition.EndTime.Value)
        {
            logger.LogInformation(
                "Recurring job {JobKey} has passed end time {EndTime}. Removing from schedule.",
                jobKey,
                definition.EndTime.Value);
            await onInvalidScheduleRemoval(jobKey);
            return null;
        }

        return definition;
    }
}
