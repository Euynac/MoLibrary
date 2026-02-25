using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.EventBus.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.ControlPlane;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Modules;
using Monica.RegisterCentre.Modules;
using Monica.Tool.MoResponse;

namespace Monica.JobScheduler.Api;

/// <summary>
/// API service for job scheduler operations.
/// Provides methods for job management, history querying, and control operations.
/// </summary>
/// <remarks>
/// This service exposes job scheduler functionality via minimal APIs.
/// It serves as the interface layer between HTTP endpoints and the job scheduler components.
/// </remarks>
public class JobSchedulerApiService(
    IJobDefinitionCacheService cacheService,
    IMoJobMetadataRepository metadataRepository,
    IJobCancellationTokenManager jobCancellationManager,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
    JobHistoryCleanupExecutor cleanupExecutor,
    IOptions<ModuleJobSchedulerOption> options,
    IOptions<ModuleRegisterCentreOption> registerCentreOptions,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoEventBus eventBus,
    ILogger<JobSchedulerApiService> logger)
{
    /// <summary>
    /// Gets all registered job definitions.
    /// </summary>
    public async Task<Res<IReadOnlyList<JobDefinition>>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("API: GetAllJobs requested");
            var jobs = await cacheService.GetAllDefinitionsAsync(cancellationToken);
            return Res.Ok(jobs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all jobs");
            return Res.Fail($"Failed to get all jobs: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new job instance for manual execution.
    /// </summary>
    public async Task<Res<string>> CreateJobInstanceAsync(
        string jobKey,
        object? jobArgs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("API: CreateJobInstance requested for {JobKey}", jobKey);

            var definition = await cacheService.GetDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }

            var centreOption = registerCentreOptions.Value;

            // On Worker nodes, delegate to Centre via event bus
            if (!centreOption.IsCentreServer && !centreOption.IsStandaloneMode)
            {
                return await CreateJobInstanceViaCentreAsync(definition, jobArgs, cancellationToken);
            }

            // Centre/Standalone: execute locally (existing flow)
            var jobArgsJson = jobArgs != null
                ? JsonSerializer.Serialize(jobArgs, options.Value.JobArgsSerializerOptions)
                : null;

            var instance = await jobInstanceManager.CreateInstanceAsync(
                definition,
                jobArgs,
                JobState.Enqueued,
                cancellationToken);

            await jobDispatcher.PublishJobExecutionEventAsync(
                instance,
                definition,
                jobArgsJson,
                cancellationToken);

            logger.LogInformation(
                "Manually triggered job instance {InstanceId} for {JobKey}",
                instance.InstanceId,
                jobKey);

            return Res.Ok(instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create job instance for {JobKey}", jobKey);
            return Res.Fail($"Failed to create job instance: {ex.Message}");
        }
    }

    private async Task<Res<string>> CreateJobInstanceViaCentreAsync(
        JobDefinition definition,
        object? jobArgs,
        CancellationToken cancellationToken)
    {
        //TODO 这里应该就创建Instances而不是到Centre创建，因为如果Centre出现问题，那无法跟踪状态。
        var instanceId = Guid.NewGuid().ToString();
        var jobArgsJson = jobArgs != null
            ? JsonSerializer.Serialize(jobArgs, options.Value.JobArgsSerializerOptions)
            : null;

        var requestEvent = new ManualJobExecutionRequestEvent
        {
            JobKey = definition.JobKey,
            JobArgsJson = jobArgsJson,
            InstanceId = instanceId,
            RequestedAt = DateTime.UtcNow
        };

        await eventBus.PublishAsync(requestEvent, null, cancellationToken);

        logger.LogInformation(
            "Delegated manual job execution to Centre: {JobKey}, InstanceId: {InstanceId}",
            definition.JobKey, instanceId);

        return Res.Ok(instanceId);
    }

    /// <summary>
    /// Gets job definitions with advanced filtering and pagination.
    /// </summary>
    public async Task<ResPaged<JobDefinition>> GetJobDefinitionsAsync(
        string? fromProject = null,
        string? jobKey = null,
        string? jobName = null,
        JobType? jobType = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("API: GetJobDefinitions requested with filters");

            var allDefinitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);

            // Apply filters in-memory
            var filtered = allDefinitions.AsEnumerable();

            if (!string.IsNullOrEmpty(fromProject))
                filtered = filtered.Where(d =>
                    d.FromProject.Contains(fromProject, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(jobKey))
                filtered = filtered.Where(d =>
                    d.JobKey.Contains(jobKey, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(jobName))
                filtered = filtered.Where(d =>
                    d.JobName.Contains(jobName, StringComparison.OrdinalIgnoreCase));

            if (jobType.HasValue)
                filtered = filtered.Where(d => d.JobType == jobType.Value);

            // Apply sorting
            if (!string.IsNullOrEmpty(sortBy))
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
            logger.LogError(ex, "Failed to get job definitions");
            return Res.Fail($"Failed to get job definitions: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets job execution history with filtering and pagination.
    /// </summary>
    public async Task<ResPaged<JobInstance>> GetJobHistoryAsync(
        string? jobKey = null,
        JobState? state = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("API: GetJobHistory requested with filters");

            var query = new JobInstanceQuery
            {
                JobKeyContains = jobKey,
                State = state,
                CreatedAfter = startTime,
                CreatedBefore = endTime,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortByCreatedAt = SortDirection.Descending
            };

            var result = await metadataRepository.QueryInstancesAsync(query, cancellationToken);

            return new ResPaged<JobInstance>(result.TotalCount, result.Items, pageNumber, pageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get job history");
            return Res.Fail($"Failed to get job history: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancels a running job instance.
    /// </summary>
    public async Task<Res> CancelJobInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("API: CancelJobInstance requested for {InstanceId}", instanceId);

            // Cancel the distributed cancellation token
            await jobCancellationManager.CancelJobTokenAsync(instanceId, cancellationToken);

            // Update instance state to Cancelled
            await jobInstanceManager.UpdateStateAsync(
                instanceId,
                JobState.Cancelled,
                "Cancelled via API",
                cancellationToken);

            return Res.Ok("Job instance cancelled successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel job instance {InstanceId}", instanceId);
            return Res.Fail($"Failed to cancel job instance: {ex.Message}");
        }
    }

    /// <summary>
    /// Pauses a recurring job.
    /// </summary>
    public async Task<Res> PauseRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await cacheService.GetDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }

            if (definition.JobType != JobType.Recurring)
            {
                return Res.Fail($"Job {jobKey} is not a recurring job");
            }

            // Update definition via cache service (write-through) and publish event
            definition.IsDisabled = true;
            await cacheService.SaveDefinitionAsync(definition, publishChangeEvent: true, cancellationToken);

            logger.LogInformation("Recurring job paused: {JobKey}", jobKey);
            return Res.Ok("Recurring job paused successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pause recurring job {JobKey}", jobKey);
            return Res.Fail($"Failed to pause recurring job: {ex.Message}");
        }
    }

    /// <summary>
    /// Resumes a paused recurring job.
    /// </summary>
    public async Task<Res> ResumeRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var definition = await cacheService.GetDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }

            if (definition.JobType != JobType.Recurring)
            {
                return Res.Fail($"Job {jobKey} is not a recurring job");
            }

            // Update definition via cache service (write-through) and publish event
            definition.IsDisabled = false;
            await cacheService.SaveDefinitionAsync(definition, publishChangeEvent: true, cancellationToken);

            logger.LogInformation("Recurring job resumed: {JobKey}", jobKey);
            return Res.Ok("Recurring job resumed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resume recurring job {JobKey}", jobKey);
            return Res.Fail($"Failed to resume recurring job: {ex.Message}");
        }
    }

    /// <summary>
    /// Batch update job state (pause/resume multiple jobs).
    /// </summary>
    /// <param name="jobKeys">List of job keys to update</param>
    /// <param name="isDisabled">Target disabled state (true = pause, false = resume)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result with detailed status for each job</returns>
    public async Task<Res<BatchJobOperationResult>> BatchUpdateJobStateAsync(
        IReadOnlyList<string> jobKeys,
        bool isDisabled,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Batch {Operation} requested for {Count} jobs",
                isDisabled ? "pause" : "resume", jobKeys.Count);

            var result = new BatchJobOperationResult
            {
                TotalRequested = jobKeys.Count
            };

            // Use SemaphoreSlim to limit concurrent operations
            var semaphore = new SemaphoreSlim(10, 10);
            var tasks = jobKeys.Select(async jobKey =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var definition = await cacheService.GetDefinitionAsync(jobKey, cancellationToken);

                    if (definition == null)
                    {
                        return new JobOperationResultItem
                        {
                            JobKey = jobKey,
                            Success = false,
                            ErrorMessage = $"Job {jobKey} not found"
                        };
                    }

                    if (definition.JobType != JobType.Recurring)
                    {
                        return new JobOperationResultItem
                        {
                            JobKey = jobKey,
                            Success = false,
                            ErrorMessage = $"Job {jobKey} is not a recurring job"
                        };
                    }

                    // Update definition via cache service (write-through + event publishing)
                    definition.IsDisabled = isDisabled;
                    await cacheService.SaveDefinitionAsync(definition, publishChangeEvent: true, cancellationToken);

                    logger.LogDebug("Job {JobKey} {Operation} successfully",
                        jobKey, isDisabled ? "paused" : "resumed");

                    return new JobOperationResultItem
                    {
                        JobKey = jobKey,
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to {Operation} job {JobKey}",
                        isDisabled ? "pause" : "resume", jobKey);

                    return new JobOperationResultItem
                    {
                        JobKey = jobKey,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var itemResults = await Task.WhenAll(tasks);
            result.Results = itemResults.ToList();
            result.SuccessCount = itemResults.Count(r => r.Success);
            result.FailedCount = itemResults.Count(r => !r.Success);

            logger.LogInformation(
                "Batch {Operation} completed: {Success} succeeded, {Failed} failed",
                isDisabled ? "pause" : "resume",
                result.SuccessCount,
                result.FailedCount);

            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Batch update job state failed");
            return Res.Fail($"Batch operation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates job configuration.
    /// </summary>
    public async Task<Res> UpdateJobConfigAsync(
        string jobKey,
        JobDefinition updatedDefinition,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("API: UpdateJobConfig requested for {JobKey}", jobKey);

            // Update via cache service (write-through)
            await cacheService.SaveDefinitionAsync(updatedDefinition, true, cancellationToken);

            logger.LogInformation("Job {JobKey} configuration updated", jobKey);
            return Res.Ok("Job configuration updated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update job config for {JobKey}", jobKey);
            return Res.Fail($"Failed to update job configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers manual history cleanup operation.
    /// </summary>
    public async Task<Res<HistoryCleanupResult>> TriggerHistoryCleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("API: TriggerHistoryCleanup requested");

            var result = await cleanupExecutor.ExecuteCleanupAsync(cancellationToken);

            logger.LogInformation(
                "Manual history cleanup completed: Deleted {DeletedCount} instances in {DurationSeconds:F2}s",
                result.DeletedCount,
                result.DurationSeconds);

            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger history cleanup");
            return Res.Fail($"Failed to trigger history cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the sort selector function for a given field name.
    /// </summary>
    private static Func<JobDefinition, object> GetSortSelector(string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "fromproject" => d => d.FromProject,
            "jobkey" => d => d.JobKey,
            "jobname" => d => d.JobName,
            "cronexpression" => d => d.CronExpression ?? string.Empty,
            "isdisabled" => d => d.IsDisabled,
            _ => d => d.JobKey // Default fallback
        };
    }
}
