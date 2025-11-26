using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业实例查询服务
/// </summary>
public class JobInstanceQueryService(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobInstanceQueryService> logger)
{
    public async Task<ResPaged<JobInstance>> GetJobInstancesAsync(
        JobInstanceFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instances = await metadataStore.GetJobInstancesAsync(
                filter.JobKey,
                filter.State,
                filter.StartTime,
                filter.EndTime,
                filter.PageNumber,
                filter.PageSize,
                cancellationToken);

            var totalCount = await metadataStore.GetJobInstancesCountAsync(
                filter.JobKey,
                filter.State,
                filter.StartTime,
                filter.EndTime,
                cancellationToken);

            // Apply InstanceId fuzzy search in-memory (if specified)
            if (!string.IsNullOrEmpty(filter.InstanceId))
            {
                instances = instances
                    .Where(i => i.InstanceId.Contains(
                        filter.InstanceId,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                totalCount = instances.Count;
            }

            return new ResPaged<JobInstance>(totalCount, instances, filter.PageNumber, filter.PageSize);
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
            var instance = await metadataStore.GetJobInstanceAsync(instanceId, cancellationToken);
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
            var instances = await metadataStore.GetJobInstancesByKeyAsync(
                jobKey,
                JobState.Processing,
                cancellationToken);
            return Res.Ok(instances);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get running instances for {JobKey}", jobKey);
            return Res.Fail($"Failed to get running instances: {ex.Message}");
        }
    }
}
