using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.TaskScheduler.Abstractions;
using MoLibrary.TaskScheduler.Models;

namespace MoLibrary.TaskScheduler.Metadata;
/// <summary>
/// Default in-memory implementation of <see cref="IMoTaskScheduleMetadataStore"/>.
/// Provides thread-safe, volatile storage for task definitions and instances using concurrent dictionaries.
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
/// <item><description>Memory usage grows with task history (no automatic cleanup)</description></item>
/// </list>
/// <para>
/// For production distributed deployments, consider implementing a persistent store using:
/// SQL Server, PostgreSQL, MongoDB, Redis, or other appropriate storage technologies.
/// </para>
/// </remarks>
public class MetadataStoreInMemoryProvider(ILogger<MetadataStoreInMemoryProvider> logger)
    : IMoTaskScheduleMetadataStore
{
    private readonly ConcurrentDictionary<string, TaskDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, TaskInstance> _instances = new();

    #region Task Definitions

    /// <inheritdoc />
    public Task<TaskDefinition?> GetTaskDefinitionAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _definitions.TryGetValue(taskKey, out var definition);
        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<IEnumerable<TaskDefinition>> GetAllTaskDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = _definitions.Values.ToList();
        return Task.FromResult<IEnumerable<TaskDefinition>>(definitions);
    }

    /// <inheritdoc />
    public Task SaveTaskDefinitionAsync(TaskDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.TaskKey))
        {
            throw new ArgumentException("TaskDefinition.TaskKey cannot be null or empty.", nameof(definition));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isNew = _definitions.TryAdd(definition.TaskKey, definition);

        if (isNew)
        {
            logger.LogInformation(
                "Task definition registered: {TaskKey} ({TaskName}), Type: {TaskType}, MaxConcurrency: {MaxConcurrency}",
                definition.TaskKey,
                definition.TaskName,
                definition.Type,
                definition.MaxConcurrency);
        }
        else
        {
            // Update existing definition
            _definitions[definition.TaskKey] = definition;

            logger.LogInformation(
                "Task definition updated: {TaskKey} ({TaskName})",
                definition.TaskKey,
                definition.TaskName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TaskDefinitionExistsAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var exists = _definitions.ContainsKey(taskKey);
        return Task.FromResult(exists);
    }

    #endregion

    #region Task Instances

    /// <inheritdoc />
    public Task<TaskInstance?> GetTaskInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
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
    public Task<IEnumerable<TaskInstance>> GetTaskInstancesByKeyAsync(
        string taskKey,
        TaskState? stateFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var instances = _instances.Values
            .Where(i => i.TaskKey == taskKey)
            .Where(i => stateFilter == null || i.State == stateFilter.Value)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        return Task.FromResult<IEnumerable<TaskInstance>>(instances);
    }

    /// <inheritdoc />
    public Task<int> GetProcessingCountAsync(string taskKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var count = _instances.Values
            .Count(i => i.TaskKey == taskKey && i.State == TaskState.Processing);

        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task SaveTaskInstanceAsync(TaskInstance instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
        {
            throw new ArgumentException("TaskInstance.InstanceId cannot be null or empty.", nameof(instance));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isNew = _instances.TryAdd(instance.InstanceId, instance);

        if (isNew)
        {
            logger.LogDebug(
                "Task instance created: {InstanceId} for {TaskKey}, State: {State}",
                instance.InstanceId,
                instance.TaskKey,
                instance.State);
        }
        else
        {
            // Update existing instance
            _instances[instance.InstanceId] = instance;

            logger.LogDebug(
                "Task instance updated: {InstanceId} for {TaskKey}, State: {State}",
                instance.InstanceId,
                instance.TaskKey,
                instance.State);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateTaskStateAsync(
        string instanceId,
        TaskState newState,
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
            throw new InvalidOperationException($"Task instance '{instanceId}' not found.");
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
            case TaskState.Processing:
                if (instance.StartedAt == null)
                {
                    instance.StartedAt = DateTime.UtcNow;
                }
                break;

            case TaskState.Succeeded:
            case TaskState.Failed:
            case TaskState.Terminated:
            case TaskState.Cancelled:
            case TaskState.Skipped:
                if (instance.CompletedAt == null)
                {
                    instance.CompletedAt = DateTime.UtcNow;
                }
                break;
        }

        // Update the instance in the dictionary (not strictly necessary with reference types, but explicit)
        _instances[instanceId] = instance;

        logger.LogInformation(
            "Task instance state transition: {InstanceId} ({TaskKey}): {OldState} → {NewState}{ErrorInfo}",
            instanceId,
            instance.TaskKey,
            oldState,
            newState,
            errorMessage != null ? $", Error: {errorMessage}" : string.Empty);

        return Task.CompletedTask;
    }

    #endregion

    #region Task History

    /// <inheritdoc />
    public Task ArchiveTaskInstanceAsync(TaskInstance instance, CancellationToken cancellationToken = default)
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
            "Task instance archived: {InstanceId} for {TaskKey}, State: {State}",
            instance.InstanceId,
            instance.TaskKey,
            instance.State);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IEnumerable<TaskInstance>> GetTaskHistoryAsync(
        string taskKey,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Task key cannot be null or empty.", nameof(taskKey));
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
            .Where(i => i.TaskKey == taskKey)
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        logger.LogDebug(
            "Retrieved task history for {TaskKey}: Page {PageNumber}, Size {PageSize}, Results: {ResultCount}",
            taskKey,
            pageNumber,
            pageSize,
            history.Count);

        return Task.FromResult<IEnumerable<TaskInstance>>(history);
    }

    #endregion
}
