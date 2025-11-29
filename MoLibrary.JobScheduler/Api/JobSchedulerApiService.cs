using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.Api;

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
    IMoJobScheduleMetadataStore metadataStore,
    [FromKeyedServices(nameof(ModuleJobScheduler))] IMoCancellationManager cancellationManager,
    JobInstanceManager jobInstanceManager,
    JobDispatcher jobDispatcher,
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
            var jobs = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);
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

            var definition = await cacheService.GetJobDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }
            
            // Create instance via JobInstanceManager
            var instance = await jobInstanceManager.CreateInstanceAsync(
                definition,
                jobArgs,
                JobState.Enqueued,
                cancellationToken);

            // Publish to event bus for worker pickup via JobDispatcher
            await jobDispatcher.PublishJobExecutionEventAsync(
                instance,
                definition,
                jobArgs,
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("API: GetJobDefinitions requested with filters");

            var allDefinitions = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);

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

            var instances = await metadataStore.GetJobInstancesAsync(
                jobKey,
                state,
                startTime,
                endTime,
                pageNumber,
                pageSize,
                cancellationToken);

            var totalCount = await metadataStore.GetJobInstancesCountAsync(
                jobKey,
                state,
                startTime,
                endTime,
                cancellationToken);

            return new ResPaged<JobInstance>(totalCount, instances, pageNumber, pageSize);
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
            await cancellationManager.CancelTokenAsync(instanceId, cancellationToken);

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
            var definition = await cacheService.GetJobDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }

            if (definition.JobType != JobType.Recurring)
            {
                return Res.Fail($"Job {jobKey} is not a recurring job");
            }

            // Update definition via cache service (write-through)
            definition.IsDisabled = true;
            await cacheService.SaveJobDefinitionAsync(definition, cancellationToken);

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
            var definition = await cacheService.GetJobDefinitionAsync(jobKey, cancellationToken);
            if (definition == null)
            {
                return Res.Fail($"Job {jobKey} not found");
            }

            if (definition.JobType != JobType.Recurring)
            {
                return Res.Fail($"Job {jobKey} is not a recurring job");
            }

            // Update definition via cache service (write-through)
            definition.IsDisabled = false;
            await cacheService.SaveJobDefinitionAsync(definition, cancellationToken);

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
            await cacheService.SaveJobDefinitionAsync(updatedDefinition, cancellationToken);

            logger.LogInformation("Job {JobKey} configuration updated", jobKey);
            return Res.Ok("Job configuration updated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update job config for {JobKey}", jobKey);
            return Res.Fail($"Failed to update job configuration: {ex.Message}");
        }
    }
}
