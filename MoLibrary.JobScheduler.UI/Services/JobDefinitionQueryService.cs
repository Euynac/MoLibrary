using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Api;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.UI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业定义查询服务
/// </summary>
public class JobDefinitionQueryService(
    JobSchedulerApiService apiService,
    ILogger<JobDefinitionQueryService> logger)
{
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
            if (result.Code != ResponseCode.Ok)
            {
                return Res.Fail($"Failed to get job definition: {result.Message}");
            }

            return Res.Ok(result.Data.Items?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job definition {JobKey}", jobKey);
            return Res.Fail($"Failed to get job definition: {ex.Message}");
        }
    }
}
