using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Metadata;

/// <summary>
/// Default in-memory implementation of <see cref="IMoJobScheduleMetadataStore"/>.
/// Provides thread-safe, volatile storage for job definitions and instances using concurrent dictionaries.
/// </summary>
public class MetadataStoreInMemoryProvider(ILogger<MetadataStoreInMemoryProvider> logger)
    : IMoJobScheduleMetadataStore
{
    private readonly ConcurrentDictionary<string, JobDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, JobInstance> _instances = new();

    #region Job Definitions

    public Task<JobDefinition?> GetJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _definitions.TryGetValue(jobKey, out var definition);
        return Task.FromResult(definition);
    }


    public Task<List<JobDefinition>> GetAllJobDefinitionsAsync(bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = includeDeleted
            ? _definitions.Values.ToList()
            : _definitions.Values.Where(d => !d.IsDeleted).ToList();

        return Task.FromResult(definitions);
    }


    public Task SaveJobDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.JobKey))
        {
            throw new ArgumentException("JobDefinition.JobKey cannot be null or empty.", nameof(definition));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isNew = _definitions.TryAdd(definition.JobKey, definition);

        if (isNew)
        {
            logger.LogInformation(
                "Job definition registered: {JobKey} ({JobName}), Type: {JobType}, MaxConcurrency: {MaxConcurrency}",
                definition.JobKey,
                definition.JobName,
                definition.JobType,
                definition.MaxConcurrency);
        }
        else
        {
            // Update existing definition
            _definitions[definition.JobKey] = definition;

            logger.LogInformation(
                "Job definition updated: {JobKey} ({JobName})",
                definition.JobKey,
                definition.JobName);
        }

        return Task.CompletedTask;
    }


    public Task<Dictionary<string, JobDefinition?>> GetJobDefinitionsByKeysAsync(
        IReadOnlyList<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        if (jobKeys == null)
        {
            throw new ArgumentNullException(nameof(jobKeys));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = new Dictionary<string, JobDefinition?>(jobKeys.Count);

        foreach (var key in jobKeys)
        {
            _definitions.TryGetValue(key, out var definition);
            result[key] = definition;
        }

        logger.LogDebug("Retrieved {Count} job definitions by keys", jobKeys.Count);

        return Task.FromResult(result);
    }


    public Task<bool> JobDefinitionExistsAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var exists = _definitions.ContainsKey(jobKey);
        return Task.FromResult(exists);
    }


    public Task SoftDeleteJobDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_definitions.TryGetValue(jobKey, out var definition))
        {
            throw new InvalidOperationException($"Job definition '{jobKey}' not found.");
        }

        definition.IsDeleted = true;
        definition.DeletedAt = DateTime.UtcNow;

        // Update the definition in the dictionary
        _definitions[jobKey] = definition;

        logger.LogInformation(
            "Job definition soft deleted: {JobKey} ({JobName})",
            definition.JobKey,
            definition.JobName);

        return Task.CompletedTask;
    }

    #endregion

    #region Job Instances

    public Task<JobInstance?> GetJobInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }


    public Task<List<JobInstance>> GetJobInstancesByKeyAsync(string jobKey,
        JobState? stateFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var instances = _instances.Values
            .Where(i => i.JobKey == jobKey)
            .Where(i => stateFilter == null || i.State == stateFilter.Value)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        return Task.FromResult(instances);
    }


    public Task SaveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
        {
            throw new ArgumentException("JobInstance.InstanceId cannot be null or empty.", nameof(instance));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isNew = _instances.TryAdd(instance.InstanceId, instance);

        if (isNew)
        {
            logger.LogDebug(
                "Job instance created: {InstanceId} for {JobKey}, State: {State}",
                instance.InstanceId,
                instance.JobKey,
                instance.State);
        }
        else
        {
            // Update existing instance
            _instances[instance.InstanceId] = instance;

            logger.LogDebug(
                "Job instance updated: {InstanceId} for {JobKey}, State: {State}",
                instance.InstanceId,
                instance.JobKey,
                instance.State);
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Job History

    public Task<IEnumerable<JobInstance>> GetJobHistoryAsync(
        string jobKey,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentException("Page size must be greater than 0.", nameof(pageSize));
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentException("Page number must be greater than 0.", nameof(pageNumber));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var skip = (pageNumber - 1) * pageSize;

        var history = _instances.Values
            .Where(i => i.JobKey == jobKey)
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        logger.LogDebug(
            "Retrieved job history for {JobKey}: Page {PageNumber}, Size {PageSize}, Results: {ResultCount}",
            jobKey,
            pageNumber,
            pageSize,
            history.Count);

        return Task.FromResult<IEnumerable<JobInstance>>(history);
    }

    #endregion
}