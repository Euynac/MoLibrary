using Microsoft.Extensions.Logging;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.ControlPlane;
using MoLibrary.JobScheduler.Models;

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
    ControlPlane.JobScheduler jobScheduler,
    JobRegistry jobRegistry,
    IMoJobScheduleMetadataStore metadataStore,
    IMoCancellationManager cancellationManager,
    JobInstanceManager jobInstanceManager,
    ILogger<JobSchedulerApiService> logger)
{
    /// <summary>
    /// Gets all registered job definitions.
    /// </summary>
    public async Task<IEnumerable<JobDefinition>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("API: GetAllJobs requested");
        return await metadataStore.GetAllJobDefinitionsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a new job instance for manual execution.
    /// </summary>
    public async Task<string> CreateJobInstanceAsync(
        string jobKey,
        string? parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: CreateJobInstance requested for {JobKey}", jobKey);

        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Job {jobKey} not found");
        }

        // For now, use EnqueueAsync with parameters
        // This is a placeholder - actual implementation would need proper parameter handling
        throw new NotImplementedException("CreateJobInstance API method not yet implemented");
    }

    /// <summary>
    /// Gets job execution history with filtering.
    /// </summary>
    public async Task<IEnumerable<JobInstance>> GetJobHistoryAsync(
        string? jobKey = null,
        JobState? state = null,
        int? limit = 100,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("API: GetJobHistory requested");

        // Placeholder - actual implementation would query metadata store with filters
        var allInstances = await metadataStore.GetAllJobDefinitionsAsync(cancellationToken: cancellationToken);
        return new List<JobInstance>();
    }

    /// <summary>
    /// Cancels a running job instance.
    /// </summary>
    public async Task CancelJobInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
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
    }

    /// <summary>
    /// Pauses a recurring job.
    /// </summary>
    public async Task PauseRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Job {jobKey} not found");
        }

        if (definition.JobType != JobType.Recurring)
        {
            throw new InvalidOperationException($"Job {jobKey} is not a recurring job");
        }

        // Update definition in metadata store
        definition.IsDisabled = true;
        await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);

        logger.LogInformation("Recurring job paused: {JobKey}", jobKey);
    }

    /// <summary>
    /// Resumes a paused recurring job.
    /// </summary>
    public async Task ResumeRecurringJobAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        var definition = await jobRegistry.GetDefinitionAsync(jobKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"Job {jobKey} not found");
        }

        if (definition.JobType != JobType.Recurring)
        {
            throw new InvalidOperationException($"Job {jobKey} is not a recurring job");
        }

        // Update definition in metadata store
        definition.IsDisabled = false;
        await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);

        logger.LogInformation("Recurring job resumed: {JobKey}", jobKey);
    }

    /// <summary>
    /// Updates job configuration.
    /// </summary>
    public async Task UpdateJobConfigAsync(
        string jobKey,
        JobDefinition updatedDefinition,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("API: UpdateJobConfig requested for {JobKey}", jobKey);

        // Update in metadata store
        await metadataStore.SaveJobDefinitionAsync(updatedDefinition, cancellationToken);

        logger.LogInformation("Job {JobKey} configuration updated", jobKey);
    }
}
