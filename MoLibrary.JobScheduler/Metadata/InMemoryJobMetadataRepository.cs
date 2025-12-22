using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Metadata;

/// <summary>
/// 基于内存的 Job 元数据仓储实现
/// </summary>
/// <remarks>
/// 使用并发字典提供线程安全的、易失性的存储。适用于开发和测试环境。
/// 数据在应用重启后会丢失。
/// </remarks>
public class InMemoryJobMetadataRepository(ILogger<InMemoryJobMetadataRepository> logger)
    : IMoJobMetadataRepository
{
    private readonly ConcurrentDictionary<string, JobDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, JobInstance> _instances = new();

    #region JobDefinition Operations

    public Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));

        cancellationToken.ThrowIfCancellationRequested();

        _definitions.TryGetValue(jobKey, out var definition);
        return Task.FromResult(definition);
    }

    public Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.JobKey))
            throw new ArgumentException("JobDefinition.JobKey cannot be null or empty.", nameof(definition));

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

    public Task<QueryResult<JobDefinition>> QueryDefinitionsAsync(
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var items = _definitions.Values.AsEnumerable();

        // 应用过滤
        if (!query.IncludeDeleted)
            items = items.Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(query.FromProject))
            items = items.Where(d => d.FromProject == query.FromProject);

        var list = items.ToList();
        var totalCount = list.Count;

        // 分页
        var paged = list
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        logger.LogDebug(
            "QueryDefinitionsAsync: Returned {Count}/{Total} definitions (Page {PageNumber}, Size {PageSize})",
            paged.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return Task.FromResult(new QueryResult<JobDefinition>(paged, totalCount));
    }

    #endregion

    #region JobInstance Operations

    public Task<JobInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));

        cancellationToken.ThrowIfCancellationRequested();

        _instances.TryGetValue(instanceId, out var instance);
        return Task.FromResult(instance);
    }

    public Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
            throw new ArgumentException("JobInstance.InstanceId cannot be null or empty.", nameof(instance));

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

    public Task<QueryResult<JobInstance>> QueryInstancesAsync(
        JobInstanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var items = _instances.Values.AsEnumerable();

        // 应用过滤
        if (!string.IsNullOrEmpty(query.JobKey))
            items = items.Where(i => i.JobKey == query.JobKey);

        if (!string.IsNullOrEmpty(query.JobKeyContains))
            items = items.Where(i => i.JobKey.Contains(query.JobKeyContains, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(query.InstanceIdContains))
            items = items.Where(i => i.InstanceId.Contains(query.InstanceIdContains, StringComparison.OrdinalIgnoreCase));

        if (query.State.HasValue)
            items = items.Where(i => i.State == query.State.Value);

        if (query.CreatedAfter.HasValue)
            items = items.Where(i => i.CreatedAt >= query.CreatedAfter.Value);

        if (query.CreatedBefore.HasValue)
            items = items.Where(i => i.CreatedAt <= query.CreatedBefore.Value);

        // 排序
        items = query.SortByCreatedAt == SortDirection.Descending
            ? items.OrderByDescending(i => i.CreatedAt)
            : items.OrderBy(i => i.CreatedAt);

        var list = items.ToList();
        var totalCount = list.Count;

        // 分页
        var paged = list
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        logger.LogDebug(
            "QueryInstancesAsync: Returned {Count}/{Total} instances (Page {PageNumber}, Size {PageSize})",
            paged.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return Task.FromResult(new QueryResult<JobInstance>(paged, totalCount));
    }

    #endregion
}
