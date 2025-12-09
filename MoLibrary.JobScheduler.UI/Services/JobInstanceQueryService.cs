using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业实例查询服务
/// </summary>
public class JobInstanceQueryService(
    IMoJobMetadataRepository metadataRepository,
    ILogger<JobInstanceQueryService> logger)
{
    public async Task<ResPaged<JobInstance>> GetJobInstancesAsync(
        JobInstanceFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new JobInstanceQuery
            {
                JobKey = filter.JobKey,
                State = filter.State,
                CreatedAfter = filter.StartTime,
                CreatedBefore = filter.EndTime,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                SortByCreatedAt = SortDirection.Descending
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            // Apply InstanceId fuzzy search in-memory (if specified)
            var items = result.Items;
            var totalCount = result.TotalCount;

            if (!string.IsNullOrEmpty(filter.InstanceId))
            {
                items = items
                    .Where(i => i.InstanceId.Contains(
                        filter.InstanceId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                totalCount = items.Count;
            }

            return new ResPaged<JobInstance>(totalCount, items, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query job instances");
            return Res.Fail($"Failed to query job instances: {ex.Message}");
        }
    }

    public async Task<Res<JobInstance?>> GetJobInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instance = await metadataRepository.GetInstanceAsync(instanceId, cancellationToken);
            return Res.Ok(instance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job instance {InstanceId}", instanceId);
            return Res.Fail($"Failed to get job instance: {ex.Message}");
        }
    }

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
                PageSize = int.MaxValue
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);
            return Res.Ok(result.Items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get running instances for {JobKey}", jobKey);
            return Res.Fail($"Failed to get running instances: {ex.Message}");
        }
    }
}
