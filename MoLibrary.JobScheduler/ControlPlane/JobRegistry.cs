using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Exceptions;
using MoLibrary.JobScheduler.Models;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Manages job definition registration and retrieval.
/// Provides methods for registering new job definitions, checking registration status,
/// and retrieving job metadata from the metadata store.
/// </summary>
public class JobRegistry(
    IJobDefinitionCacheService cacheService,
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobRegistry> logger)
{
    private readonly Dictionary<Type, Type> _triggeredJobMapping = new();
    private readonly Dictionary<string, Type> _jobDefinitionTypeMap = new();
    private readonly Dictionary<string, Type> _triggeredJobArgsTypeMap = new();

    /// <summary>
    ///    Registers a job type to be executed
    /// </summary>
    public Task RegisterJob(JobDefinition jobDefinition, string status)
    {
        logger.LogInformation(
            "[{Status}] Job: {JobKey} | Name: {JobName} | Type: {JobType} | MaxConcurrency: {MaxConcurrency} | RetryCount: {RetryCount} | Timeout: {Timeout}",
            status,
            jobDefinition.JobKey,
            jobDefinition.JobName,
            jobDefinition.JobType,
            jobDefinition.MaxConcurrency,
            jobDefinition.RetryCount,
            jobDefinition.MaxExecutionTimeout);
        _jobDefinitionTypeMap.Add(jobDefinition.JobClrType.FullName!, jobDefinition.JobClrType);
        
        if (jobDefinition.JobType == JobType.Recurring)
        {
            logger.LogDebug(
                "Recurring job details - JobKey: {JobKey}, CronExpression: {CronExpression}, StartTime: {StartTime}, EndTime: {EndTime}, IsDisabled: {IsDisabled}",
                jobDefinition.JobKey,
                jobDefinition.CronExpression,
                jobDefinition.StartTime,
                jobDefinition.EndTime,
                jobDefinition.IsDisabled);
        }
        else if (jobDefinition.JobType == JobType.Triggered)
        {
            if (jobDefinition.JobArgsClrType == null)
                throw new JobRegistrationException(jobDefinition.JobKey, "ParameterClrType is null");
            _triggeredJobMapping.Add(jobDefinition.JobArgsClrType, jobDefinition.JobClrType);
            _triggeredJobArgsTypeMap.Add(jobDefinition.JobArgsClrType.FullName!, jobDefinition.JobArgsClrType);
            logger.LogDebug(
                "Triggered job details - JobKey: {JobKey}, ParameterType: {ParameterType}",
                jobDefinition.JobKey,
                jobDefinition.JobArgsClrType.GetCleanFullName());
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves the CLR type for a job by its job key.
    /// </summary>
    /// <param name="jobKey">The unique job key (job type's full name).</param>
    /// <returns>The CLR type of the job, or null if not found.</returns>
    public Type? GetJobClrType(string jobKey)
    {
        return _jobDefinitionTypeMap.TryGetValue(jobKey, out var type) ? type : null;
    }

    /// <summary>
    /// Retrieves the CLR type for job arguments by its job args key.
    /// </summary>
    /// <param name="jobArgsKey">The unique job args key (args type's full name).</param>
    /// <returns>The CLR type of the job arguments, or null if not found.</returns>
    public Type? GetJobArgsClrType(string jobArgsKey)
    {
        return _triggeredJobArgsTypeMap.TryGetValue(jobArgsKey, out var type) ? type : null;
    }

  
    /// <summary>
    /// Reconciles the current job definitions with the metadata store.
    /// Adds new jobs that are not yet registered and soft deletes jobs that no longer exist.
    /// Does NOT update existing job properties.
    /// </summary>
    /// <param name="currentDefinitions">The current collection of job definitions from code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result object containing details about added and deleted jobs.</returns>
    public async Task<JobReconciliationResult> ReconcileJobDefinitionsAsync(
        IReadOnlyList<JobDefinition> currentDefinitions,
        CancellationToken cancellationToken = default)
    {
        if (currentDefinitions == null)
        {
            throw new ArgumentNullException(nameof(currentDefinitions));
        }

        logger.LogInformation("Starting job definition reconciliation for {Count} current job(s)", currentDefinitions.Count);

        // Get all existing non-deleted job definitions from cache (initializes cache if needed)
        var existingDefinitions = await cacheService.GetAllJobDefinitionsAsync(cancellationToken);
        var existingJobKeys = existingDefinitions.Select(d => d.JobKey).ToHashSet();
        var currentJobKeys = currentDefinitions.Select(d => d.JobKey).ToHashSet();

        // Find jobs to add: in current definitions but not in metadata store
        var jobsToAdd = currentDefinitions
            .Where(d => !existingJobKeys.Contains(d.JobKey))
            .ToList();

        // Find jobs to soft delete: in metadata store but not in current definitions
        var jobKeysToDelete = existingJobKeys
            .Where(key => !currentJobKeys.Contains(key))
            .ToList();

        logger.LogInformation(
            "Reconciliation analysis: {AddCount} job(s) to add, {DeleteCount} job(s) to soft delete",
            jobsToAdd.Count,
            jobKeysToDelete.Count);

        // Add new jobs
        var addedJobKeys = new List<string>();
        foreach (var definition in jobsToAdd)
        {
            if (string.IsNullOrWhiteSpace(definition.JobKey))
            {
                logger.LogWarning("Skipping job with null or empty JobKey: {JobName}", definition.JobName);
                continue;
            }

            await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);
            addedJobKeys.Add(definition.JobKey);

            logger.LogInformation(
                "Job added: {JobKey} ({JobName}), Type: {JobType}",
                definition.JobKey,
                definition.JobName,
                definition.JobType);
        }

        // Soft delete removed jobs
        var deletedJobKeys = new List<string>();
        foreach (var jobKey in jobKeysToDelete)
        {
            try
            {
                var definition = await metadataStore.GetJobDefinitionAsync(jobKey, cancellationToken);
                if (definition == null)
                {
                    logger.LogWarning("Job not found for soft delete: {JobKey}", jobKey);
                    continue;
                }
                definition.IsDeleted = true;
                definition.DeletedAt = DateTime.UtcNow;
                definition.JobKey =  $"[deleted]{jobKey}";
                
                await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);
                deletedJobKeys.Add(jobKey);

                logger.LogInformation("Job soft deleted: {JobKey}", jobKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to soft delete job: {JobKey}", jobKey);
            }
        }

        var result = new JobReconciliationResult
        {
            AddedCount = addedJobKeys.Count,
            DeletedCount = deletedJobKeys.Count,
            AddedJobKeys = addedJobKeys,
            DeletedJobKeys = deletedJobKeys
        };

        logger.LogInformation(
            "Job definition reconciliation completed: {AddedCount} added, {DeletedCount} deleted",
            result.AddedCount,
            result.DeletedCount);

        return result;
    }
}

/// <summary>
/// Result of job definition reconciliation operation.
/// Contains information about jobs that were added and soft deleted.
/// </summary>
public class JobReconciliationResult
{
    /// <summary>
    /// Gets or sets the count of job definitions that were added.
    /// </summary>
    public int AddedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of job definitions that were soft deleted.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Gets or sets the list of job keys that were added during reconciliation.
    /// </summary>
    public IReadOnlyList<string> AddedJobKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of job keys that were soft deleted during reconciliation.
    /// </summary>
    public IReadOnlyList<string> DeletedJobKeys { get; set; } = [];
}
