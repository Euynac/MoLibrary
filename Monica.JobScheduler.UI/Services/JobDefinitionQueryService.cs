using Cronos;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Monica.Modules;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Api;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.UI.Models;
using Monica.Tool.MoResponse;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// 作业定义查询服务
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

            // ResPaged 失败时，Code 不是 Ok
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
    /// 获取带有上次执行信息的作业定义列表
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

            // 批量获取所有作业的最后执行实例（一次查询，避免N+1问题）
            var jobKeys = definitions.Select(d => d.JobKey).ToList();
            var lastExecutionMap = await metadataRepository.GetLatestInstancesAsync(jobKeys, cancellationToken);

            var enhancedJobs = new List<JobDefinitionWithLastExecution>();

            foreach (var definition in definitions)
            {
                var enhanced = new JobDefinitionWithLastExecution
                {
                    Definition = definition
                };

                // 从批量查询结果中获取上次执行实例
                enhanced.LastExecution = lastExecutionMap.GetValueOrDefault(definition.JobKey);

                // 计算下次执行时间（仅针对 RecurringJob）
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
    /// 计算 RecurringJob 的下次执行时间
    /// </summary>
    private DateTime? CalculateNextExecutionTime(string cronExpression, DateTime? startTime, DateTime? endTime)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, Cronos.CronFormat.IncludeSeconds);
            var now = DateTime.UtcNow;

            // 如果有开始时间限制，使用开始时间和当前时间中较晚的时间
            var fromTime = startTime.HasValue && startTime.Value > now ? startTime.Value : now;

            var nextOccurrence = cron.GetNextOccurrence(fromTime, _cronTimeZone);

            // 检查是否超过结束时间
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
