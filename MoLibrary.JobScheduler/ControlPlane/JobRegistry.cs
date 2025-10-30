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
/// <remarks>
/// <para>
/// JobRegistry acts as the central coordinator for job definition management in the job scheduler system.
/// It ensures job definitions are properly validated and persisted before jobs can be scheduled or executed.
/// </para>
/// <para>
/// <b>Registration Process:</b>
/// </para>
/// <list type="number">
/// <item><description>Module initialization discovers job types during startup</description></item>
/// <item><description>Job keys are checked against the registry to identify unregistered jobs</description></item>
/// <item><description>Job definitions are created from job metadata (attributes, reflection)</description></item>
/// <item><description>Registry validates uniqueness and persists definitions to metadata store</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b>
/// The registry delegates persistence to IMoJobScheduleMetadataStore, which must provide thread-safe operations.
/// Multiple workers can safely call GetDefinitionAsync concurrently.
/// </para>
/// </remarks>
public class JobRegistry(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<JobRegistry> logger)
{
    /// <summary>
    /// Checks which job keys from the provided collection are not yet registered in the metadata store.
    /// </summary>
    /// <param name="jobKeys">
    /// Collection of job keys to check for registration status.
    /// Job keys are typically the full type name of the job class.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains a collection of job keys that are not yet registered.
    /// Returns an empty collection if all provided job keys are already registered.
    /// </returns>
    /// <remarks>
    /// This method is used during module initialization to identify which jobs need to be registered.
    /// It performs efficient existence checks without loading full job definitions.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when jobKeys is null.</exception>
    /// <example>
    /// <code>
    /// var discoveredKeys = new[] { "MyApp.Jobs.EmailJob", "MyApp.Jobs.ReportJob" };
    /// var unregistered = await jobRegistry.CheckUnregisteredAsync(discoveredKeys);
    /// // unregistered contains only keys that need to be registered
    /// </code>
    /// </example>
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
    /// <param name="definitions">
    /// Collection of job definitions to register.
    /// Each definition must have a unique JobKey.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs the following validations:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Checks for duplicate JobKeys within the provided collection</description></item>
    /// <item><description>Checks for conflicts with existing registered jobs</description></item>
    /// <item><description>Logs all successful registrations for audit purposes</description></item>
    /// </list>
    /// <para>
    /// If any validation fails, the operation throws JobRegistrationException with details about the conflict.
    /// The exception includes the conflicting JobKey for troubleshooting.
    /// </para>
    /// <para>
    /// <b>Important:</b> This method does not cache job definitions. All subsequent lookups
    /// will query the metadata store directly to ensure consistency in distributed deployments.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when definitions is null.</exception>
    /// <exception cref="JobRegistrationException">
    /// Thrown when duplicate job keys are detected within the collection or when
    /// attempting to register a job that already exists in the metadata store.
    /// </exception>
    /// <example>
    /// <code>
    /// var definitions = new[]
    /// {
    ///     new JobDefinition
    ///     {
    ///         JobKey = "MyApp.Jobs.EmailJob",
    ///         JobName = "Email Sender",
    ///         Type = JobType.Triggered,
    ///         MaxConcurrency = 5
    ///     }
    /// };
    ///
    /// try
    /// {
    ///     await jobRegistry.RegisterJobsAsync(definitions);
    /// }
    /// catch (JobRegistrationException ex)
    /// {
    ///     Console.WriteLine($"Registration failed for job: {ex.JobKey}");
    /// }
    /// </code>
    /// </example>
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
    /// <param name="jobKey">
    /// The unique identifier for the job.
    /// Typically the full type name of the job class.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// The task result contains the JobDefinition if found, or null if no job with the specified key exists.
    /// </returns>
    /// <remarks>
    /// This method does not cache job definitions and always queries the metadata store.
    /// This ensures consistency in distributed deployments where job definitions may be updated
    /// via the Control Plane API.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when jobKey is null or empty.</exception>
    /// <example>
    /// <code>
    /// var definition = await jobRegistry.GetDefinitionAsync("MyApp.Jobs.EmailJob");
    /// if (definition != null)
    /// {
    ///     Console.WriteLine($"Job: {definition.JobName}");
    ///     Console.WriteLine($"Max Concurrency: {definition.MaxConcurrency}");
    /// }
    /// </code>
    /// </example>
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
