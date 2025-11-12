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
}
