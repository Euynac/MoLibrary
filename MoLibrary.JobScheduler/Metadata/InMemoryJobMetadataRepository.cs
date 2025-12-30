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

        // 排序 (flexible approach)
        if (!string.IsNullOrEmpty(query.SortBy))
        {
            items = ApplyInstanceSorting(items, query.SortBy, query.SortDescending);
        }
        else
        {
            items = items.OrderByDescending(i => i.CreatedAt);
        }

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

    /// <summary>
    /// 批量获取多个作业的最后一次执行实例（避免N+1查询问题）
    /// </summary>
    public Task<Dictionary<string, JobInstance?>> GetLatestInstancesAsync(
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobKeys);

        var jobKeyList = jobKeys.ToList();
        var result = new Dictionary<string, JobInstance?>();

        if (jobKeyList.Count == 0)
        {
            return Task.FromResult(result);
        }

        // 对每个JobKey找到最后一次执行实例
        foreach (var jobKey in jobKeyList)
        {
            var latestInstance = _instances.Values
                .Where(i => i.JobKey == jobKey)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefault();

            result[jobKey] = latestInstance;
        }

        logger.LogDebug(
            "GetLatestInstancesAsync: Retrieved latest instances for {Count} jobs, found {FoundCount} instances",
            jobKeyList.Count,
            result.Count(r => r.Value != null));

        return Task.FromResult(result);
    }

    /// <summary>
    /// 批量删除 Job 实例（内存实现）
    /// </summary>
    public Task<int> DeleteInstancesAsync(
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        cancellationToken.ThrowIfCancellationRequested();

        var instanceIdList = instanceIds.ToList();
        var deletedCount = 0;

        foreach (var instanceId in instanceIdList)
        {
            if (_instances.TryRemove(instanceId, out var removedInstance))
            {
                deletedCount++;
                logger.LogDebug(
                    "Deleted job instance: {InstanceId} for {JobKey}",
                    instanceId,
                    removedInstance.JobKey);
            }
        }

        logger.LogInformation(
            "Batch deleted {DeletedCount}/{RequestedCount} job instances",
            deletedCount,
            instanceIdList.Count);

        return Task.FromResult(deletedCount);
    }

    /// <summary>
    /// 查询需要清理的实例ID列表（优化的批量清理查询）
    /// </summary>
    public Task<List<string>> GetCleanupCandidatesAsync(
        IReadOnlyDictionary<string, (int MaxRecords, int? MaxDays)> retentionPolicies,
        int maxRetainedOrphanedInstances = 10,
        int maxDeletionsPerCycle = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retentionPolicies);
        cancellationToken.ThrowIfCancellationRequested();

        var terminalStates = new HashSet<JobState>
        {
            JobState.Succeeded, JobState.Terminated,
            JobState.Cancelled, JobState.Skipped, JobState.Failed
        };

        var now = DateTime.UtcNow;
        var candidateIds = new HashSet<string>();

        // Single pass: group by JobKey, project only needed fields
        var groupedInstances = _instances.Values
            .Where(i => terminalStates.Contains(i.State))
            .GroupBy(i => i.JobKey)
            .Select(g => new
            {
                JobKey = g.Key,
                Instances = g.Select(i => new
                {
                    i.InstanceId,
                    SortDate = i.CompletedAt ?? i.CreatedAt
                }).OrderByDescending(x => x.SortDate).ToList()
            });

        foreach (var group in groupedInstances)
        {
            int maxRecords;
            int? maxDays;

            if (retentionPolicies.TryGetValue(group.JobKey, out var policy))
            {
                maxRecords = policy.MaxRecords > 0 ? policy.MaxRecords : int.MaxValue;
                maxDays = policy.MaxDays;
            }
            else
            {
                // Orphaned - use default
                maxRecords = maxRetainedOrphanedInstances > 0 ? maxRetainedOrphanedInstances : int.MaxValue;
                maxDays = null;
            }

            var cutoffDate = maxDays.HasValue ? now.AddDays(-maxDays.Value) : (DateTime?)null;

            for (var i = 0; i < group.Instances.Count; i++)
            {
                var instance = group.Instances[i];
                var shouldDelete = false;

                // Count-based: beyond maxRecords limit
                if (i >= maxRecords)
                {
                    shouldDelete = true;
                }
                // Time-based: older than cutoff
                else if (cutoffDate.HasValue && instance.SortDate < cutoffDate.Value)
                {
                    shouldDelete = true;
                }

                if (shouldDelete)
                {
                    candidateIds.Add(instance.InstanceId);
                }
            }
        }

        // Apply per-cycle limit
        var result = maxDeletionsPerCycle > 0 && candidateIds.Count > maxDeletionsPerCycle
            ? candidateIds.Take(maxDeletionsPerCycle).ToList()
            : candidateIds.ToList();

        logger.LogDebug(
            "GetCleanupCandidatesAsync: Found {Count} candidates (limit: {Limit})",
            result.Count,
            maxDeletionsPerCycle > 0 ? maxDeletionsPerCycle.ToString() : "unlimited");

        return Task.FromResult(result);
    }

    /// <summary>
    /// Applies dynamic sorting to JobInstance enumerable based on field name
    /// </summary>
    private static IEnumerable<JobInstance> ApplyInstanceSorting(
        IEnumerable<JobInstance> items,
        string sortBy,
        bool descending)
    {
        return sortBy switch
        {
            "InstanceId" => descending
                ? items.OrderByDescending(i => i.InstanceId)
                : items.OrderBy(i => i.InstanceId),
            "JobKey" => descending
                ? items.OrderByDescending(i => i.JobKey)
                : items.OrderBy(i => i.JobKey),
            "State" => descending
                ? items.OrderByDescending(i => i.State)
                : items.OrderBy(i => i.State),
            "CreatedAt" => descending
                ? items.OrderByDescending(i => i.CreatedAt)
                : items.OrderBy(i => i.CreatedAt),
            "StartedAt" => descending
                ? items.OrderByDescending(i => i.StartedAt ?? DateTime.MinValue)
                : items.OrderBy(i => i.StartedAt ?? DateTime.MinValue),
            "CompletedAt" => descending
                ? items.OrderByDescending(i => i.CompletedAt ?? DateTime.MinValue)
                : items.OrderBy(i => i.CompletedAt ?? DateTime.MinValue),
            "Duration" => descending
                ? items.OrderByDescending(i =>
                    i.CompletedAt.HasValue && i.StartedAt.HasValue
                        ? (i.CompletedAt.Value - i.StartedAt.Value).TotalSeconds
                        : (i.StartedAt.HasValue ? (DateTime.UtcNow - i.StartedAt.Value).TotalSeconds : 0))
                : items.OrderBy(i =>
                    i.CompletedAt.HasValue && i.StartedAt.HasValue
                        ? (i.CompletedAt.Value - i.StartedAt.Value).TotalSeconds
                        : (i.StartedAt.HasValue ? (DateTime.UtcNow - i.StartedAt.Value).TotalSeconds : 0)),
            _ => descending
                ? items.OrderByDescending(i => i.CreatedAt)
                : items.OrderBy(i => i.CreatedAt)
        };
    }

    #endregion
}
