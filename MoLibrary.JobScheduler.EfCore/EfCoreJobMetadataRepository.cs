using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.EfCore.Entities;
using MoLibrary.JobScheduler.EfCore.Mappers;
using MoLibrary.JobScheduler.Metadata;
using MoLibrary.JobScheduler.Models;
using MoLibrary.Repository.Interfaces;

namespace MoLibrary.JobScheduler.EfCore;

/// <summary>
/// EF Core implementation of IMoJobMetadataRepository.
/// Thread-safe through scoped DbContext pattern using IDbContextProvider.
/// </summary>
public class EfCoreJobMetadataRepository(
    IDbContextProvider<JobSchedulerDbContext> dbContextProvider,
    ILogger<EfCoreJobMetadataRepository> logger) : IMoJobMetadataRepository
{
    #region JobDefinition Operations

    public async Task<JobDefinition?> GetDefinitionAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var entity = await dbContext.JobDefinitions
            .AsNoTracking()
            .IgnoreQueryFilters() // Include soft-deleted for lookups  TODO in future, unified filter disable logic 
            .FirstOrDefaultAsync(e => e.JobKey == jobKey, cancellationToken);

        return entity == null ? null : JobMetadataMapper.ToModel(entity);
    }

    public async Task SaveDefinitionAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.JobKey))
            throw new ArgumentException("JobDefinition.JobKey cannot be null or empty.", nameof(definition));

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var existingEntity = await dbContext.JobDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.JobKey == definition.JobKey, cancellationToken);

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

        IQueryable<JobDefinitionEntity> queryable = dbContext.JobDefinitions.AsNoTracking();

        // Apply soft delete filter conditionally
        if (!query.IncludeDeleted)
        {
            queryable = queryable.Where(d => !d.IsDeleted);
        }
        else
        {
            queryable = queryable.IgnoreQueryFilters();
        }

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
            .FirstOrDefaultAsync(e => e.InstanceId == instanceId, cancellationToken);

        return entity == null ? null : JobMetadataMapper.ToModel(entity);
    }

    public async Task SaveInstanceAsync(JobInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
            throw new ArgumentException("JobInstance.InstanceId cannot be null or empty.", nameof(instance));

        var dbContext = await dbContextProvider.GetDbContextAsync();

        var existingEntity = await dbContext.JobInstances
            .FirstOrDefaultAsync(e => e.InstanceId == instance.InstanceId, cancellationToken);

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

        IQueryable<JobInstanceEntity> queryable = dbContext.JobInstances.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrEmpty(query.JobKey))
        {
            queryable = queryable.Where(i => i.JobKey == query.JobKey);
        }

        if (!string.IsNullOrEmpty(query.JobKeyContains))
        {
            queryable = queryable.Where(i => EF.Functions.Like(i.JobKey, $"%{query.JobKeyContains}%"));
        }

        if (!string.IsNullOrEmpty(query.InstanceIdContains))
        {
            queryable = queryable.Where(i => EF.Functions.Like(i.InstanceId, $"%{query.InstanceIdContains}%"));
        }

        if (query.State.HasValue)
        {
            queryable = queryable.Where(i => i.State == query.State.Value);
        }

        if (query.CreatedAfter.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.CreatedBefore.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt <= query.CreatedBefore.Value);
        }

        // Get total count
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply sorting
        queryable = query.SortByCreatedAt == SortDirection.Descending
            ? queryable.OrderByDescending(i => i.CreatedAt)
            : queryable.OrderBy(i => i.CreatedAt);

        // Apply pagination
        var entities = await queryable
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(JobMetadataMapper.ToModel).ToList();

        logger.LogDebug(
            "QueryInstancesAsync: Returned {Count}/{Total} instances (Page {PageNumber}, Size {PageSize})",
            items.Count,
            totalCount,
            query.PageNumber,
            query.PageSize);

        return new QueryResult<JobInstance>(items, totalCount);
    }

    #endregion
}
