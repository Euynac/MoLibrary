using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Models;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Owns the immutable local job-type catalog and reconciles leader-owned job metadata with the metadata store.
/// </summary>
/// <remarks>
/// The local catalog is built when this singleton is created on every host, including Worker hosts that never acquire
/// service-discovery leadership. Persistent reconciliation remains a control-plane operation invoked only by the
/// leader-coordinated registration service.
/// </remarks>
public class JobRegistry(
    IJobDefinitionCacheService cacheService,
    IJobMetadataRepository metadataRepository,
    IReadOnlyList<JobDefinition> localJobDefinitions,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobRegistry> logger)
{
    private readonly LocalJobCatalog _localJobs = LocalJobCatalog.Create(localJobDefinitions, logger);

    /// <summary>
    /// Retrieves the CLR type for a job by its job key.
    /// </summary>
    /// <param name="jobKey">The unique job key (job type's full name).</param>
    /// <returns>The CLR type of the job, or null if not found.</returns>
    public Type? GetJobClrType(string jobKey)
    {
        return _localJobs.JobTypesByKey.GetValueOrDefault(jobKey);
    }
    
    /// <summary>
    /// Retrieves the CLR type for a triggered job by its job args key.
    /// </summary>
    /// <param name="jobArgsKey"></param>
    /// <returns></returns>
    public Type? GetTriggeredJobClrType(string jobArgsKey)
    {
        return _localJobs.TriggeredJobTypesByArgsKey.GetValueOrDefault(jobArgsKey);
    }

    /// <summary>
    /// Retrieves the CLR type for job arguments by its job args key.
    /// </summary>
    /// <param name="jobArgsKey">The unique job args key (args type's full name).</param>
    /// <returns>The CLR type of the job arguments, or null if not found.</returns>
    public Type? GetJobArgsClrType(string jobArgsKey)
    {
        return _localJobs.TriggeredArgsTypesByKey.GetValueOrDefault(jobArgsKey);
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

        var currentProjectName = options.Value.GetProjectName();

        // Get all existing non-deleted job definitions from cache (initializes cache if needed)
        var existingDefinitions = await cacheService.GetAllDefinitionsAsync(cancellationToken);

        // Filter to only this project's jobs
        var existingProjectJobs = existingDefinitions
            .Where(d => d.FromProject == currentProjectName)
            .ToList();

        var existingJobKeys = existingProjectJobs.Select(d => d.JobKey).ToHashSet();
        var currentJobKeys = currentDefinitions.Select(d => d.JobKey).ToHashSet();

        // Find jobs to add: in current definitions but not in metadata store
        var jobsToAdd = currentDefinitions
            .Where(d => !existingJobKeys.Contains(d.JobKey))
            .ToList();

        // Find jobs to soft delete: in metadata store but not in current definitions (scoped to current project)
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

            await metadataRepository.SaveDefinitionAsync(definition, cancellationToken);
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
                var definition = await metadataRepository.GetDefinitionAsync(jobKey, cancellationToken);
                if (definition == null)
                {
                    logger.LogWarning("Job not found for soft delete: {JobKey}", jobKey);
                    continue;
                }
                definition.IsDeleted = true;
                definition.DeletedAt = DateTime.UtcNow;
                
                await metadataRepository.SaveDefinitionAsync(definition, cancellationToken);
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

    private sealed record LocalJobCatalog(
        FrozenDictionary<string, Type> JobTypesByKey,
        FrozenDictionary<string, Type> TriggeredJobTypesByArgsKey,
        FrozenDictionary<string, Type> TriggeredArgsTypesByKey)
    {
        internal static LocalJobCatalog Create(
            IReadOnlyList<JobDefinition> definitions,
            ILogger<JobRegistry> logger)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            var jobTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);
            var triggeredJobTypesByArgsKey = new Dictionary<string, Type>(StringComparer.Ordinal);
            var triggeredArgsTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                if (!jobTypesByKey.TryAdd(definition.JobKey, definition.JobClrType))
                {
                    throw new JobRegistrationException(
                        $"Duplicate local job key '{definition.JobKey}'.",
                        definition.JobKey);
                }

                logger.LogInformation(
                    "Registered local job: {JobKey} | Name: {JobName} | Type: {JobType} | MaxConcurrency: {MaxConcurrency} | RetryCount: {RetryCount} | Timeout: {Timeout}",
                    definition.JobKey,
                    definition.JobName,
                    definition.JobType,
                    definition.MaxConcurrency,
                    definition.RetryCount,
                    definition.MaxExecutionTimeout);

                if (definition.JobType == JobType.Recurring)
                {
                    logger.LogDebug(
                        "Recurring job details - JobKey: {JobKey}, CronExpression: {CronExpression}, StartTime: {StartTime}, EndTime: {EndTime}, IsDisabled: {IsDisabled}",
                        definition.JobKey,
                        definition.CronExpression,
                        definition.StartTime,
                        definition.EndTime,
                        definition.IsDisabled);
                    continue;
                }

                if (definition.JobType != JobType.Triggered)
                {
                    continue;
                }

                var argumentsType = definition.JobArgsClrType
                    ?? throw new JobRegistrationException(
                        $"Triggered job '{definition.JobKey}' does not declare an argument CLR type.",
                        definition.JobKey);
                var argumentsKey = argumentsType.FullName
                    ?? throw new JobRegistrationException(
                        $"Argument CLR type '{argumentsType.Name}' for job '{definition.JobKey}' has no full name.",
                        definition.JobKey);

                triggeredJobTypesByArgsKey.TryAdd(argumentsKey, definition.JobClrType);
                triggeredArgsTypesByKey.TryAdd(argumentsKey, argumentsType);
                logger.LogDebug(
                    "Triggered job details - JobKey: {JobKey}, ParameterType: {ParameterType}",
                    definition.JobKey,
                    argumentsType.GetCleanFullName());
            }

            return new LocalJobCatalog(
                jobTypesByKey.ToFrozenDictionary(StringComparer.Ordinal),
                triggeredJobTypesByArgsKey.ToFrozenDictionary(StringComparer.Ordinal),
                triggeredArgsTypesByKey.ToFrozenDictionary(StringComparer.Ordinal));
        }
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
