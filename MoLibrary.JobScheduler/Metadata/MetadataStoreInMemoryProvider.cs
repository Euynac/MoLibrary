using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Metadata;
/// <summary>
/// Default in-memory implementation of <see cref="IMoJobScheduleMetadataStore"/>.
/// Provides thread-safe, volatile storage for job definitions and instances using concurrent dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is suitable for:
/// </para>
/// <list type="bullet">
/// <item><description><b>Development:</b> Fast, simple setup without external dependencies</description></item>
/// <item><description><b>Testing:</b> Isolated test environments with predictable state</description></item>
/// <item><description><b>Single-Instance Production:</b> Low-volume scenarios where data persistence is not critical</description></item>
/// </list>
/// <para>
/// <b>Limitations:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Data is lost on application restart (no persistence)</description></item>
/// <item><description>Not suitable for distributed deployments (no shared state across instances)</description></item>
/// <item><description>Memory usage grows with job history (no automatic cleanup)</description></item>
/// </list>
/// <para>
/// For production distributed deployments, consider implementing a persistent store using:
/// SQL Server, PostgreSQL, MongoDB, Redis, or other appropriate storage technologies.
/// </para>
/// </remarks>
public class MetadataStoreInMemoryProvider(ILogger<MetadataStoreInMemoryProvider> logger)
    : IMoJobScheduleMetadataStore
{
    private readonly ConcurrentDictionary<string, JobDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, JobInstance> _instances = new();

    #region Job Definitions

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<IEnumerable<JobDefinition>> GetAllJobDefinitionsAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = includeDeleted
            ? _definitions.Values.ToList()
            : _definitions.Values.Where(d => !d.IsDeleted).ToList();

        return Task.FromResult<IEnumerable<JobDefinition>>(definitions);
    }

    /// <inheritdoc />
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
                definition.Type,
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<IEnumerable<JobInstance>> GetJobInstancesByKeyAsync(
        string jobKey,
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

        return Task.FromResult<IEnumerable<JobInstance>>(instances);
    }

    /// <inheritdoc />
    public Task<int> GetProcessingCountAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var count = _instances.Values
            .Count(i => i.JobKey == jobKey && i.State == JobState.Processing);

        return Task.FromResult(count);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task UpdateJobStateAsync(
        string instanceId,
        JobState newState,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_instances.TryGetValue(instanceId, out var instance))
        {
            throw new InvalidOperationException($"Job instance '{instanceId}' not found.");
        }

        var oldState = instance.State;
        instance.State = newState;

        // Set error message if provided
        if (errorMessage != null)
        {
            instance.ErrorMessage = errorMessage;
        }

        // Update timestamps based on state transitions
        switch (newState)
        {
            case JobState.Processing:
                if (instance.StartedAt == null)
                {
                    instance.StartedAt = DateTime.UtcNow;
                }
                break;

            case JobState.Succeeded:
            case JobState.Failed:
            case JobState.Terminated:
            case JobState.Cancelled:
            case JobState.Skipped:
                if (instance.CompletedAt == null)
                {
                    instance.CompletedAt = DateTime.UtcNow;
                }
                break;
        }

        // Update the instance in the dictionary (not strictly necessary with reference types, but explicit)
        _instances[instanceId] = instance;

        logger.LogInformation(
            "Job instance state transition: {InstanceId} ({JobKey}): {OldState} → {NewState}{ErrorInfo}",
            instanceId,
            instance.JobKey,
            oldState,
            newState,
            errorMessage != null ? $", Error: {errorMessage}" : string.Empty);

        return Task.CompletedTask;
    }

    #endregion

    #region Job History

    /// <inheritdoc />
    public Task ArchiveJobInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // In the in-memory implementation, archiving is a no-op since we keep everything in the same dictionary
        // In a real implementation, this might move the instance to a separate archive storage
        // or mark it with an archived flag

        logger.LogDebug(
            "Job instance archived: {InstanceId} for {JobKey}, State: {State}",
            instance.InstanceId,
            instance.JobKey,
            instance.State);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
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
