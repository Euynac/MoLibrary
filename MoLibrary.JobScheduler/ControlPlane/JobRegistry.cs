using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Exceptions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.ControlPlane;

/// <summary>
/// Manages job definition registration and retrieval.
/// Provides methods for registering new job definitions, checking registration status,
/// and retrieving job metadata from the metadata store.
/// </summary>
public class JobRegistry(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobRegistry> logger)
{
    /// <summary>
    /// Checks which job keys from the provided collection are not yet registered in the metadata store.
    /// </summary>
    public async Task<IEnumerable<string>> CheckUnregisteredAsync(
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        if (jobKeys == null)
        {
            throw new ArgumentNullException(nameof(jobKeys));
        }

        var jobKeysList = jobKeys.ToList();
        var unregistered = new List<string>();

        foreach (var jobKey in jobKeysList)
        {
            if (string.IsNullOrWhiteSpace(jobKey))
            {
                logger.LogWarning("Skipping empty or null job key during registration check");
                continue;
            }

            var exists = await metadataStore.JobDefinitionExistsAsync(jobKey, cancellationToken);
            if (!exists)
            {
                unregistered.Add(jobKey);
            }
        }

        logger.LogDebug(
            "Registration check completed: {TotalCount} keys checked, {UnregisteredCount} unregistered",
            jobKeysList.Count,
            unregistered.Count);

        return unregistered;
    }

    /// <summary>
    /// Registers multiple job definitions in the metadata store.
    /// Validates that all job keys are unique and throws an exception if duplicates are detected.
    /// </summary>
    public async Task RegisterJobsAsync(
        IEnumerable<JobDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        var definitionsList = definitions.ToList();

        // Check for duplicate keys within the provided collection
        var duplicates = definitionsList
            .GroupBy(d => d.JobKey)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            var duplicateKeys = string.Join(", ", duplicates);
            var message = $"Duplicate job keys detected in registration batch: {duplicateKeys}";

            logger.LogError(message);
            throw new JobRegistrationException(message, duplicates.First());
        }

        // Check for conflicts with existing registered jobs
        foreach (var definition in definitionsList)
        {
            if (string.IsNullOrWhiteSpace(definition.JobKey))
            {
                logger.LogError("Attempted to register job with null or empty JobKey: {JobName}", definition.JobName);
                throw new JobRegistrationException("Job definition must have a non-empty JobKey");
            }

            var exists = await metadataStore.JobDefinitionExistsAsync(definition.JobKey, cancellationToken);
            if (exists)
            {
                var message = $"Job with key '{definition.JobKey}' is already registered. " +
                             $"Cannot register duplicate job definitions.";

                logger.LogError(
                    "Duplicate job registration attempt: {JobKey} ({JobName})",
                    definition.JobKey,
                    definition.JobName);

                throw new JobRegistrationException(message, definition.JobKey);
            }
        }

        // Register all definitions
        foreach (var definition in definitionsList)
        {
            await metadataStore.SaveJobDefinitionAsync(definition, cancellationToken);

            logger.LogInformation(
                "Job registered: {JobKey} ({JobName}), Type: {JobType}, MaxConcurrency: {MaxConcurrency}, RetryCount: {RetryCount}",
                definition.JobKey,
                definition.JobName,
                definition.Type,
                definition.MaxConcurrency,
                definition.RetryCount);
        }

        logger.LogInformation(
            "Successfully registered {Count} job definition(s)",
            definitionsList.Count);
    }

    /// <summary>
    /// Retrieves a job definition by its unique job key.
    /// </summary>
    public async Task<JobDefinition?> GetDefinitionAsync(
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        var definition = await metadataStore.GetJobDefinitionAsync(jobKey, cancellationToken);

        if (definition == null)
        {
            logger.LogWarning("Job definition not found: {JobKey}", jobKey);
        }

        return definition;
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

        // Get all existing non-deleted job definitions from the metadata store
        var existingDefinitions = await metadataStore.GetAllJobDefinitionsAsync(includeDeleted: false, cancellationToken);
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
                definition.Type);
        }

        // Soft delete removed jobs
        var deletedJobKeys = new List<string>();
        foreach (var jobKey in jobKeysToDelete)
        {
            try
            {
                await metadataStore.SoftDeleteJobDefinitionAsync(jobKey, cancellationToken);
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
    public IReadOnlyList<string> AddedJobKeys { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the list of job keys that were soft deleted during reconciliation.
    /// </summary>
    public IReadOnlyList<string> DeletedJobKeys { get; set; } = Array.Empty<string>();
}
