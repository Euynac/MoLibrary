using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.EfCore.Mappers;
using Monica.JobScheduler.EfCore.Support;
using Monica.JobScheduler.Models;
using Monica.Modules;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// EF Core implementation of IJobMetadataRepository.
/// Thread-safe through scoped DbContext pattern using IDbContextProvider.
/// </summary>
public class EfCoreJobMetadataRepository(
    IDbContextProvider<JobSchedulerDbContext> dbContextProvider,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<EfCoreJobMetadataRepository> logger) : IJobMetadataRepository
{
    private readonly string _schedulerScopeKey = options.Value.SchedulerScopeKey;

    #region JobDefinition Operations

    public async Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var entity = await dbContext.JobDefinitions
            .AsNoTracking()
            .IgnoreQueryFilters() // Include soft-deleted for lookups  TODO in future, unified filter disable logic 
            .FirstOrDefaultAsync(
                e => e.JobKey == jobKey && e.SchedulerScopeKey == _schedulerScopeKey,
                cancellationToken);

        return entity == null ? null : JobMetadataMapper.ToModel(entity);
    }

    public async Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.JobKey))
            throw new ArgumentException("JobDefinition.JobKey cannot be null or empty.", nameof(definition));

        var dbContext = await dbContextProvider.GetDbContextAsync();
        definition.SchedulerScopeKey = _schedulerScopeKey;

        var existingEntity = await dbContext.JobDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.JobKey == definition.JobKey && e.SchedulerScopeKey == _schedulerScopeKey,
                cancellationToken);

        if (existingEntity == null)
        {
            // Create new
            var newEntity = JobMetadataMapper.ToEntity(definition);
            dbContext.JobDefinitions.Add(newEntity);

            logger.LogInformation(
                "Job definition registered: {JobKey} ({JobName}), Type: {JobType}, MaxConcurrency: {MaxConcurrency}",
                definition.JobKey,
                definition.JobName,
                definition.JobType,
                definition.MaxConcurrency);
        }
        else
        {
            // Update existing
            JobMetadataMapper.ToEntity(definition, existingEntity);
            if (existingEntity.IsDeleted)
            {
                existingEntity.JobKey =  $"[deleted-{Guid.NewGuid()}]{existingEntity.JobKey}";
            }

            logger.LogInformation(
                "Job definition updated: {JobKey} ({JobName})",
                definition.JobKey,
                definition.JobName);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueryResult<JobDefinition>> QueryDefinitionsAsync(
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var queryable = dbContext.JobDefinitions.AsNoTracking();
        queryable = queryable.Where(d => d.SchedulerScopeKey == _schedulerScopeKey);

        // Apply soft delete filter conditionally
        queryable = !query.IncludeDeleted ? queryable.Where(d => !d.IsDeleted) : queryable.IgnoreQueryFilters();

        // Apply project filter
        if (!string.IsNullOrEmpty(query.FromProject))
        {
            queryable = queryable.Where(d => d.FromProject == query.FromProject);
        }

        // Get total count
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply pagination
        var entities = await queryable
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(JobMetadataMapper.ToModel).ToList();

        logger.LogDebug(
            "QueryDefinitionsAsync: Returned {Count}/{Total} definitions (Page {PageNumber}, Size {PageSize})",
            items.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return new QueryResult<JobDefinition>(items, totalCount);
    }

    #endregion

    #region JobInstance Operations

    public async Task<JobInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instance ID cannot be null or empty.", nameof(instanceId));

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var entity = await dbContext.JobInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.InstanceId == instanceId && e.SchedulerScopeKey == _schedulerScopeKey,
                cancellationToken);

        return entity == null ? null : JobMetadataMapper.ToModel(entity);
    }

    public async Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
            throw new ArgumentException("JobInstance.InstanceId cannot be null or empty.", nameof(instance));

        var dbContext = await dbContextProvider.GetDbContextAsync();
        instance.SchedulerScopeKey = _schedulerScopeKey;

        var existingEntity = await dbContext.JobInstances
            .FirstOrDefaultAsync(
                e => e.InstanceId == instance.InstanceId && e.SchedulerScopeKey == _schedulerScopeKey,
                cancellationToken);

        if (existingEntity == null)
        {
            // Create new
            var newEntity = JobMetadataMapper.ToEntity(instance);
            dbContext.JobInstances.Add(newEntity);

            logger.LogDebug(
                "Job instance created: {InstanceId} for {JobKey}, State: {State}",
                instance.InstanceId,
                instance.JobKey,
                instance.State);
        }
        else
        {
            // Update existing
            JobMetadataMapper.ToEntity(instance, existingEntity);

            logger.LogDebug(
                "Job instance updated: {InstanceId} for {JobKey}, State: {State}",
                instance.InstanceId,
                instance.JobKey,
                instance.State);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueryResult<JobInstance>> QueryInstancesAsync(
        JobInstanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var queryable = dbContext.JobInstances
            .AsNoTracking()
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey)
            .ApplyFilters(query);

        // Get total count
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply sorting and pagination
        var entities = await queryable
            .ApplySorting(query.SortBy, query.SortDescending)
            .ApplyPaginationAsync(query.PageNumber, query.PageSize, cancellationToken);

        var items = entities.Select(JobMetadataMapper.ToModel).ToList();

        logger.LogDebug(
            "QueryInstancesAsync: Returned {Count}/{Total} instances (Page {PageNumber}, Size {PageSize})",
            items.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return new QueryResult<JobInstance>(items, totalCount);
    }

    /// <summary>
    /// Queries job instances and projects them to a custom type using database-side projection.
    /// </summary>
    public async Task<QueryResult<TResult>> QueryInstancesAsync<TResult>(
        JobInstanceQuery query,
        Expression<Func<JobInstance, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var queryable = dbContext.JobInstances
            .AsNoTracking()
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey)
            .ApplyFilters(query);

        // Get total count before projection
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply sorting
        queryable = queryable.ApplySorting(query.SortBy, query.SortDescending);

        // Rewrite expression from JobInstance to JobInstanceEntity for database-side projection
        var entitySelector = JobInstanceExpressionRewriter.Rewrite(selector);

        // Apply pagination and projection (database-side SELECT)
        var items = await queryable
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(entitySelector)
            .ToListAsync(cancellationToken);

        logger.LogDebug(
            "QueryInstancesAsync<TResult>: Returned {Count}/{Total} projected instances (Page {PageNumber}, Size {PageSize})",
            items.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return new QueryResult<TResult>(items, totalCount);
    }

    /// <summary>
    /// Gets the instance count for each state within the specified time range using database-side grouping.
    /// </summary>
    public async Task<Dictionary<JobState, int>> GetStateStatisticsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();

        // Database-side GROUP BY - generates efficient SQL
        var statistics = await dbContext.JobInstances
            .AsNoTracking()
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey)
            .ApplyTimeRangeFilter(startTime, endTime)
            .GroupBy(i => i.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Convert to dictionary, ensuring all states are represented
        var result = new Dictionary<JobState, int>();
        foreach (var state in Enum.GetValues<JobState>())
        {
            result[state] = statistics.FirstOrDefault(s => s.State == state)?.Count ?? 0;
        }

        logger.LogDebug(
            "GetStateStatisticsAsync: Retrieved statistics for {StateCount} states, total {TotalCount} instances",
            statistics.Count,
            statistics.Sum(s => s.Count));

        return result;
    }

    /// <summary>
    /// Gets the latest execution instance for each requested job in a single query.
    /// </summary>
    public async Task<Dictionary<string, JobInstance?>> GetLatestInstancesAsync(
        IEnumerable<string> jobKeys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobKeys);

        var jobKeyList = jobKeys.ToList();
        if (jobKeyList.Count == 0)
        {
            return new Dictionary<string, JobInstance?>();
        }

        var dbContext = await dbContextProvider.GetDbContextAsync();

        // Use grouped subqueries to find the latest instance per job key.
        var latestInstances = await dbContext.JobInstances
            .AsNoTracking()
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey && jobKeyList.Contains(i.JobKey))
            .GroupBy(i => i.JobKey)
            .Select(g => g.OrderByDescending(i => i.CreatedAt).First())
            .ToListAsync(cancellationToken);

        // Convert to dictionary.
        var result = new Dictionary<string, JobInstance?>();
        foreach (var jobKey in jobKeyList)
        {
            var instance = latestInstances.FirstOrDefault(i => i.JobKey == jobKey);
            result[jobKey] = instance == null ? null : JobMetadataMapper.ToModel(instance);
        }

        logger.LogDebug(
            "GetLatestInstancesAsync: Retrieved latest instances for {Count} jobs, found {FoundCount} instances",
            jobKeyList.Count,
            latestInstances.Count);

        return result;
    }

    /// <summary>
    /// Deletes job instances in batch using EF Core.
    /// </summary>
    public async Task<int> DeleteInstancesAsync(
        IEnumerable<string> instanceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        var instanceIdList = instanceIds.ToList();
        if (instanceIdList.Count == 0)
        {
            return 0;
        }

        var dbContext = await dbContextProvider.GetDbContextAsync();

        // Use ExecuteDeleteAsync for efficient batch deletion (EF Core 7+)
        // This generates a single DELETE statement without loading entities into memory
        var deletedCount = await dbContext.JobInstances
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey && instanceIdList.Contains(i.InstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "Batch deleted {DeletedCount} job instances (requested {RequestedCount})",
            deletedCount,
            instanceIdList.Count);

        return deletedCount;
    }

    /// <summary>
    /// Queries the instance identifiers that should be cleaned up.
    /// </summary>
    public async Task<List<string>> GetCleanupCandidatesAsync(
        IReadOnlyDictionary<string, (int MaxRecords, int? MaxDays)> retentionPolicies,
        int maxRetainedOrphanedInstances = 10,
        int maxDeletionsPerCycle = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retentionPolicies);

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var terminalStates = new[]
        {
            JobState.Succeeded, JobState.Terminated,
            JobState.Cancelled, JobState.Skipped, JobState.Failed
        };

        var now = DateTime.UtcNow;

        // Step 1: Query terminal instances with projection (only needed fields)
        // This avoids loading large StateHistory strings
        var rankedQuery = dbContext.JobInstances
            .AsNoTracking()
            .Where(i => i.SchedulerScopeKey == _schedulerScopeKey && terminalStates.Contains(i.State))
            .Select(i => new
            {
                i.InstanceId,
                i.JobKey,
                SortDate = i.CompletedAt ?? i.CreatedAt
            });

        // Materialize with projection (much lighter than full entities)
        var allInstances = await rankedQuery.ToListAsync(cancellationToken);

        // Step 2: In-memory ranking and filtering (on lightweight objects)
        var candidateIds = new HashSet<string>();

        var groupedInstances = allInstances
            .GroupBy(i => i.JobKey)
            .Select(g => new
            {
                JobKey = g.Key,
                Instances = g.OrderByDescending(x => x.SortDate).ToList()
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

        logger.LogInformation(
            "GetCleanupCandidatesAsync: Found {Count} cleanup candidates from {TotalInstances} terminal instances",
            result.Count,
            allInstances.Count);

        return result;
    }
    #endregion
}
