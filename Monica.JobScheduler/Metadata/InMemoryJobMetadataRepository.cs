using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.Modules;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Metadata;

/// <summary>
/// In-memory implementation of the job metadata repository.
/// </summary>
/// <remarks>
/// Uses concurrent dictionaries to provide thread-safe volatile storage for development and test environments.
/// Data is lost when the application restarts.
/// </remarks>
public class InMemoryJobMetadataRepository(
    ILogger<InMemoryJobMetadataRepository> logger,
    IOptions<ModuleJobSchedulerOption> options)
    : IMoJobMetadataRepository
{
    private readonly string _schedulerScopeKey = options.Value.SchedulerScopeKey;
    private readonly ConcurrentDictionary<string, JobDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, JobInstance> _instances = new();

    #region JobDefinition Operations

    public Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));

        cancellationToken.ThrowIfCancellationRequested();

        _definitions.TryGetValue(jobKey, out var definition);
        return Task.FromResult(IsCurrentScope(definition?.SchedulerScopeKey) ? definition : null);
    }

    public Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (string.IsNullOrWhiteSpace(definition.JobKey))
            throw new ArgumentException("JobDefinition.JobKey cannot be null or empty.", nameof(definition));

        cancellationToken.ThrowIfCancellationRequested();
        definition.SchedulerScopeKey = _schedulerScopeKey;

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

        var items = _definitions.Values
            .Where(d => IsCurrentScope(d.SchedulerScopeKey))
            .AsEnumerable();

        // Apply filters
        if (!query.IncludeDeleted)
            items = items.Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(query.FromProject))
            items = items.Where(d => d.FromProject == query.FromProject);

        var list = items.ToList();
        var totalCount = list.Count;

        // Apply pagination
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
        return Task.FromResult(IsCurrentScope(instance?.SchedulerScopeKey) ? instance : null);
    }

    public Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
            throw new ArgumentException("JobInstance.InstanceId cannot be null or empty.", nameof(instance));

        cancellationToken.ThrowIfCancellationRequested();
        instance.SchedulerScopeKey = _schedulerScopeKey;

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

        var items = _instances.Values
            .Where(i => IsCurrentScope(i.SchedulerScopeKey))
            .ApplyFilters(query)
            .ApplySorting(query.SortBy, query.SortDescending);

        var list = items.ToList();
        var totalCount = list.Count;
        var paged = list.ApplyPagination(query.PageNumber, query.PageSize);

        logger.LogDebug(
            "QueryInstancesAsync: Returned {Count}/{Total} instances (Page {PageNumber}, Size {PageSize})",
            paged.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return Task.FromResult(new QueryResult<JobInstance>(paged, totalCount));
    }

    /// <summary>
    /// Queries job instances and projects them to a custom type.
    /// </summary>
    public Task<QueryResult<TResult>> QueryInstancesAsync<TResult>(
        JobInstanceQuery query,
        Expression<Func<JobInstance, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);
        cancellationToken.ThrowIfCancellationRequested();

        var items = _instances.Values
            .Where(i => IsCurrentScope(i.SchedulerScopeKey))
            .ApplyFilters(query)
            .ApplySorting(query.SortBy, query.SortDescending);

        var list = items.ToList();
        var totalCount = list.Count;

        // Compile and apply projection
        var compiled = selector.Compile();
        var projected = list
            .ApplyPagination(query.PageNumber, query.PageSize)
            .Select(compiled)
            .ToList();

        logger.LogDebug(
            "QueryInstancesAsync<TResult>: Returned {Count}/{Total} projected instances (Page {PageNumber}, Size {PageSize})",
            projected.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return Task.FromResult(new QueryResult<TResult>(projected, totalCount));
    }

    /// <summary>
    /// Gets the instance count for each state within the specified time range.
    /// </summary>
    public Task<Dictionary<JobState, int>> GetStateStatisticsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var statistics = _instances.Values
            .Where(i => IsCurrentScope(i.SchedulerScopeKey))
            .ApplyTimeRangeFilter(startTime, endTime)
            .GroupBy(i => i.State)
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure all states are represented
        var result = new Dictionary<JobState, int>();
        foreach (var state in Enum.GetValues<JobState>())
        {
            result[state] = statistics.GetValueOrDefault(state, 0);
        }

        logger.LogDebug(
            "GetStateStatisticsAsync: Retrieved statistics for time range, total {TotalCount} instances",
            result.Values.Sum());

        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets the latest execution instance for each requested job without N+1 lookups.
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

        // Find the latest execution instance for each job key.
        foreach (var jobKey in jobKeyList)
        {
            var latestInstance = _instances.Values
                .Where(i => IsCurrentScope(i.SchedulerScopeKey) && i.JobKey == jobKey)
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
    /// Deletes job instances in batch using the in-memory store.
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
            if (_instances.TryGetValue(instanceId, out var instance) &&
                IsCurrentScope(instance.SchedulerScopeKey) &&
                _instances.TryRemove(instanceId, out var removedInstance))
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
    /// Queries the instance identifiers that should be cleaned up.
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
            .Where(i => IsCurrentScope(i.SchedulerScopeKey))
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

    private bool IsCurrentScope(string? schedulerScopeKey)
    {
        return string.Equals(schedulerScopeKey, _schedulerScopeKey, StringComparison.Ordinal);
    }

    #endregion
}
