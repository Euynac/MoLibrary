using Cronos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.Modules;

namespace Monica.JobScheduler.Facades;

/// <summary>
/// Query-oriented entry points for Job Scheduler definitions, instances, and summary cards.
/// </summary>
public class JobSchedulerQueryFacade(
    IJobDefinitionCacheService cacheService,
    IJobMetadataRepository metadataRepository,
    IOptions<ModuleClockOption> clockOptions,
    ILogger<JobSchedulerQueryFacade> logger)
{
    private readonly TimeZoneInfo _cronTimeZone = clockOptions.Value.ConfiguredTimeZone ?? TimeZoneInfo.Local;

    /// <summary>
    /// Gets job definitions with filtering and pagination.
    /// </summary>
    public async Task<ResPaged<JobDefinition>> GetJobDefinitionsAsync(
        string? fromProject = null,
        string? jobKey = null,
        string? jobName = null,
        string? searchText = null,
        IReadOnlyCollection<string>? fromProjects = null,
        bool? isDisabled = null,
        JobType? jobType = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allDefinitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var filtered = ApplyDefinitionFilters(
                allDefinitions,
                fromProject,
                jobKey,
                jobName,
                searchText,
                fromProjects,
                isDisabled,
                jobType);

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                filtered = sortDescending
                    ? filtered.OrderByDescending(GetSortSelector(sortBy))
                    : filtered.OrderBy(GetSortSelector(sortBy));
            }

            var totalCount = filtered.Count();
            var items = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new ResPaged<JobDefinition>(totalCount, items, pageNumber, pageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job definitions");
            return Res.Fail($"Failed to query job definitions: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets one job definition by key.
    /// </summary>
    public async Task<Res<JobDefinition?>> GetJobDefinitionAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Res.Ok(await cacheService.GetDefinitionAsync(jobKey, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job definition {JobKey}", jobKey);
            return Res.Fail($"Failed to get job definition: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets job definitions enriched with last execution data and next run estimate.
    /// </summary>
    public async Task<ResPaged<JobDefinitionWithLastExecution>> GetJobDefinitionsWithLastExecutionAsync(
        string? fromProject = null,
        string? jobKey = null,
        string? jobName = null,
        string? searchText = null,
        IReadOnlyCollection<string>? fromProjects = null,
        bool? isDisabled = null,
        bool onlyLastExecutionFailed = false,
        JobType? jobType = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allDefinitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            var definitions = ApplyDefinitionFilters(
                    allDefinitions,
                    fromProject,
                    jobKey,
                    jobName,
                    searchText,
                    fromProjects,
                    isDisabled,
                    jobType)
                .ToList();

            var latestInstances = await metadataRepository.GetLatestInstancesAsync(
                definitions.Select(d => d.JobKey),
                cancellationToken);

            var items = definitions
                .Select(definition => new JobDefinitionWithLastExecution
                {
                    Definition = definition,
                    LastExecution = latestInstances.GetValueOrDefault(definition.JobKey),
                    NextExecutionTime = definition.JobType == JobType.Recurring && !string.IsNullOrWhiteSpace(definition.CronExpression)
                        ? CalculateNextExecutionTime(definition.CronExpression, definition.StartTime, definition.EndTime)
                        : null
                })
                .ToList();

            if (onlyLastExecutionFailed)
            {
                items = items
                    .Where(item => item.LastExecution?.State is JobState.Failed or JobState.Terminated)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                items = ApplyDefinitionWithLastExecutionSorting(items, sortBy, sortDescending).ToList();
            }

            var totalCount = items.Count;
            items = items
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new ResPaged<JobDefinitionWithLastExecution>(
                totalCount,
                items,
                pageNumber,
                pageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job definitions with last execution");
            return Res.Fail($"Failed to query job definitions: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets job instances with filtering and pagination.
    /// </summary>
    public async Task<ResPaged<JobInstance>> GetJobInstancesAsync(
        string? jobKey = null,
        string? instanceId = null,
        string? searchText = null,
        JobState? state = null,
        IReadOnlyCollection<JobState>? states = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                JobKeyContains = jobKey,
                InstanceIdContains = instanceId,
                SearchText = searchText,
                State = state,
                States = states is { Count: > 0 } ? states.ToList() : null,
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            return new ResPaged<JobInstance>(result.TotalCount, result.Items, pageNumber, pageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job instances");
            return Res.Fail($"Failed to query job instances: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets one job instance by instance identifier.
    /// </summary>
    public async Task<Res<JobInstance?>> GetJobInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Res.Ok(await metadataRepository.GetInstanceAsync(instanceId, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job instance {InstanceId}", instanceId);
            return Res.Fail($"Failed to get job instance: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets currently running instances for one job definition.
    /// </summary>
    public async Task<Res<List<JobInstance>>> GetRunningInstancesAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                JobKey = jobKey,
                State = JobState.Processing,
                PageNumber = 1,
                PageSize = int.MaxValue,
                SortBy = "CreatedAt",
                SortDescending = true
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            return Res.Ok(result.Items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get running instances for {JobKey}", jobKey);
            return Res.Fail($"Failed to get running instances: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets execution statistics for one job definition.
    /// </summary>
    public async Task<Res<JobStatistics>> GetJobStatisticsAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                JobKey = jobKey,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var allInstances = result.Items;

            var statistics = new JobStatistics
            {
                JobKey = jobKey,
                TotalExecutions = allInstances.Count,
                FailedExecutions = allInstances.Count(i => i.State is JobState.Failed or JobState.Terminated)
            };

            var completedInstances = allInstances
                .Where(i => i.StartedAt.HasValue && i.CompletedAt.HasValue)
                .Select(i => new
                {
                    i.InstanceId,
                    Duration = i.CompletedAt!.Value - i.StartedAt!.Value
                })
                .ToList();

            if (completedInstances.Count > 0)
            {
                statistics = new JobStatistics
                {
                    JobKey = statistics.JobKey,
                    TotalExecutions = statistics.TotalExecutions,
                    FailedExecutions = statistics.FailedExecutions,
                    AverageExecutionTime = TimeSpan.FromTicks((long)completedInstances.Average(i => i.Duration.Ticks)),
                    FastestExecutionTime = completedInstances.MinBy(i => i.Duration)?.Duration,
                    FastestInstanceId = completedInstances.MinBy(i => i.Duration)?.InstanceId,
                    SlowestExecutionTime = completedInstances.MaxBy(i => i.Duration)?.Duration,
                    SlowestInstanceId = completedInstances.MaxBy(i => i.Duration)?.InstanceId,
                    LastExecutionTime = allInstances.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.CreatedAt,
                    LastExecutionState = allInstances.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.State
                };
            }
            else
            {
                var lastExecution = allInstances.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
                statistics = new JobStatistics
                {
                    JobKey = statistics.JobKey,
                    TotalExecutions = statistics.TotalExecutions,
                    FailedExecutions = statistics.FailedExecutions,
                    LastExecutionTime = lastExecution?.CreatedAt,
                    LastExecutionState = lastExecution?.State
                };
            }

            return Res.Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate statistics for {JobKey}", jobKey);
            return Res.Fail($"Failed to calculate statistics: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets health metrics for one job definition within a configurable window.
    /// </summary>
    public async Task<Res<JobHealthMetrics>> GetJobHealthMetricsAsync(
        string jobKey,
        TimeSpan metricsWindow,
        int recentFailureLimit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - metricsWindow;
            var query = new JobInstanceQuery
            {
                JobKey = jobKey,
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = 1,
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            var instances = result.Items;
            var stateGroups = instances.GroupBy(i => i.State).ToDictionary(g => g.Key, g => g.Count());

            return Res.Ok(new JobHealthMetrics
            {
                JobKey = jobKey,
                TotalExecutions = instances.Count,
                SucceededCount = stateGroups.GetValueOrDefault(JobState.Succeeded, 0),
                SkippedCount = stateGroups.GetValueOrDefault(JobState.Skipped, 0),
                CancelledCount = stateGroups.GetValueOrDefault(JobState.Cancelled, 0),
                ProcessingCount = stateGroups.GetValueOrDefault(JobState.Processing, 0),
                FailedCount = stateGroups.GetValueOrDefault(JobState.Failed, 0),
                TerminatedCount = stateGroups.GetValueOrDefault(JobState.Terminated, 0),
                EnqueuedCount = stateGroups.GetValueOrDefault(JobState.Enqueued, 0),
                ScheduledCount = stateGroups.GetValueOrDefault(JobState.Scheduled, 0),
                RecentFailures = instances
                    .Where(i => i.State is JobState.Failed or JobState.Terminated)
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(recentFailureLimit)
                    .ToList(),
                MetricsStartTime = startTime,
                MetricsEndTime = endTime
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate health metrics for {JobKey}", jobKey);
            return Res.Fail($"Failed to calculate health metrics: {ex.GetMessageRecursively()}");
        }
    }

    private DateTime? CalculateNextExecutionTime(string cronExpression, DateTime? startTime, DateTime? endTime)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            var now = DateTime.UtcNow;
            var fromTime = startTime.HasValue && startTime.Value > now ? startTime.Value : now;
            var nextOccurrence = cron.GetNextOccurrence(fromTime, _cronTimeZone);

            if (nextOccurrence.HasValue && endTime.HasValue && nextOccurrence.Value > endTime.Value)
            {
                return null;
            }

            return nextOccurrence;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to calculate next execution time for cron expression {CronExpression}", cronExpression);
            return null;
        }
    }

    private static IEnumerable<JobDefinition> ApplyDefinitionFilters(
        IReadOnlyList<JobDefinition> definitions,
        string? fromProject,
        string? jobKey,
        string? jobName,
        string? searchText,
        IReadOnlyCollection<string>? fromProjects,
        bool? isDisabled,
        JobType? jobType)
    {
        var filtered = definitions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(fromProject))
        {
            filtered = filtered.Where(d => d.FromProject.Contains(fromProject, StringComparison.OrdinalIgnoreCase));
        }

        if (fromProjects is { Count: > 0 })
        {
            filtered = filtered.Where(d => fromProjects.Contains(d.FromProject));
        }

        if (!string.IsNullOrWhiteSpace(jobKey))
        {
            filtered = filtered.Where(d => d.JobKey.Contains(jobKey, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(jobName))
        {
            filtered = filtered.Where(d => d.JobName.Contains(jobName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(d =>
                d.FromProject.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                d.JobKey.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                d.JobName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (isDisabled.HasValue)
        {
            filtered = filtered.Where(d => d.IsDisabled == isDisabled.Value);
        }

        if (jobType.HasValue)
        {
            filtered = filtered.Where(d => d.JobType == jobType.Value);
        }

        return filtered;
    }

    private static IEnumerable<JobDefinitionWithLastExecution> ApplyDefinitionWithLastExecutionSorting(
        IEnumerable<JobDefinitionWithLastExecution> items,
        string sortBy,
        bool descending)
    {
        return sortBy switch
        {
            "FromProject" => descending
                ? items.OrderByDescending(x => x.Definition.FromProject)
                : items.OrderBy(x => x.Definition.FromProject),
            "JobKey" => descending
                ? items.OrderByDescending(x => x.Definition.JobKey)
                : items.OrderBy(x => x.Definition.JobKey),
            "JobName" => descending
                ? items.OrderByDescending(x => x.Definition.JobName)
                : items.OrderBy(x => x.Definition.JobName),
            "CronExpression" => descending
                ? items.OrderByDescending(x => x.Definition.CronExpression ?? string.Empty)
                : items.OrderBy(x => x.Definition.CronExpression ?? string.Empty),
            "IsDisabled" => descending
                ? items.OrderByDescending(x => x.Definition.IsDisabled)
                : items.OrderBy(x => x.Definition.IsDisabled),
            "NextExecutionTime" => descending
                ? items.OrderByDescending(x => x.NextExecutionTime ?? DateTime.MinValue)
                : items.OrderBy(x => x.NextExecutionTime ?? DateTime.MinValue),
            "LastExecution" => descending
                ? items.OrderByDescending(x => x.LastExecution?.CreatedAt ?? DateTime.MinValue)
                : items.OrderBy(x => x.LastExecution?.CreatedAt ?? DateTime.MinValue),
            _ => items
        };
    }

    private static Func<JobDefinition, object> GetSortSelector(string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "fromproject" => d => d.FromProject,
            "jobkey" => d => d.JobKey,
            "jobname" => d => d.JobName,
            "cronexpression" => d => d.CronExpression ?? string.Empty,
            "isdisabled" => d => d.IsDisabled,
            _ => d => d.JobKey
        };
    }
}
