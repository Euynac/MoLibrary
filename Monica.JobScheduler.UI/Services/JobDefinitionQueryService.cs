using Cronos;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Monica.Modules;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Api;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Tool.Results;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// Job definition query service
/// </summary>
public class JobDefinitionQueryService(
    JobSchedulerApiService apiService,
    IMoJobMetadataRepository metadataRepository,
    IOptions<ModuleClockOption> clockOptions,
    ILogger<JobDefinitionQueryService> logger)
{
    private readonly TimeZoneInfo _cronTimeZone = clockOptions.Value.ConfiguredTimeZone ?? TimeZoneInfo.Local;

    public async Task<ResPaged<JobDefinition>> GetJobDefinitionsAsync(
        JobDefinitionFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await apiService.GetJobDefinitionsAsync(
                filter.FromProject,
                filter.JobKey,
                filter.JobName,
                filter.JobType,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job definitions");
            return Res.Fail($"Failed to query job definitions: {ex.Message}");
        }
    }

    public async Task<Res<JobDefinition?>> GetJobDefinitionAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await apiService.GetJobDefinitionsAsync(
                jobKey: jobKey,
                pageNumber: 1,
                pageSize: 1,
                cancellationToken: cancellationToken);

            // ResPaged fails when Code is not Ok
            if (result.IsFailed(out var error))
            {
                return Res.Fail($"Failed to get job definition: {error.Message}");
            }

            return Res.Ok(result.Data.Items?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job definition {JobKey}", jobKey);
            return Res.Fail($"Failed to get job definition: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a list of job definitions with last execution information
    /// </summary>
    public async Task<ResPaged<JobDefinitionWithLastExecution>> GetJobDefinitionsWithLastExecutionAsync(
        JobDefinitionFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var definitionsResult = await apiService.GetJobDefinitionsAsync(
                filter.FromProject,
                filter.JobKey,
                filter.JobName,
                filter.JobType,
                filter.PageNumber,
                filter.PageSize,
                filter.SortBy,
                filter.SortDescending,
                cancellationToken);

            if (definitionsResult.IsFailed(out var error, out var pageData))
            {
                return Res.Fail($"Failed to query job definitions: {error.Message}");
            }

            var definitions = pageData.Items ?? [];

            // Obtain the last execution instances of all jobs in batches (one query to avoid the N+1 problem)
            var jobKeys = definitions.Select(d => d.JobKey).ToList();
            var lastExecutionMap = await metadataRepository.GetLatestInstancesAsync(jobKeys, cancellationToken);

            var enhancedJobs = new List<JobDefinitionWithLastExecution>();

            foreach (var definition in definitions)
            {
                var enhanced = new JobDefinitionWithLastExecution
                {
                    Definition = definition
                };

                // Get the last execution instance from batch query results
                enhanced.LastExecution = lastExecutionMap.GetValueOrDefault(definition.JobKey);

                // Calculate the next execution time (only for RecurringJob)
                if (definition.JobType == JobType.Recurring && !string.IsNullOrEmpty(definition.CronExpression))
                {
                    enhanced.NextExecutionTime = CalculateNextExecutionTime(definition.CronExpression, definition.StartTime, definition.EndTime);
                }

                enhancedJobs.Add(enhanced);
            }

            return new ResPaged<JobDefinitionWithLastExecution>(
                pageData.Sum ?? 0,
                enhancedJobs,
                filter.PageNumber,
                filter.PageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job definitions with last execution");
            return Res.Fail($"Failed to query job definitions: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate the next execution time of RecurringJob
    /// </summary>
    private DateTime? CalculateNextExecutionTime(string cronExpression, DateTime? startTime, DateTime? endTime)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, Cronos.CronFormat.IncludeSeconds);
            var now = DateTime.UtcNow;

            // If there is a start time limit, use the later of the start time and the current time
            var fromTime = startTime.HasValue && startTime.Value > now ? startTime.Value : now;

            var nextOccurrence = cron.GetNextOccurrence(fromTime, _cronTimeZone);

            // Check if the end time is exceeded
            if (nextOccurrence.HasValue && endTime.HasValue && nextOccurrence.Value > endTime.Value)
            {
                return null;
            }

            return nextOccurrence;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to calculate next execution time for cron expression: {CronExpression}", cronExpression);
            return null;
        }
    }
}
