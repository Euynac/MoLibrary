using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Metadata;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// Extension methods for applying JobInstanceQuery filters to IQueryable{JobInstanceEntity}.
/// Used by EfCoreJobMetadataRepository.
/// </summary>
public static class JobInstanceEntityQueryExtensions
{
    /// <summary>
    /// Applies all filters from JobInstanceQuery to the queryable.
    /// Uses EF.Functions.Like for Contains operations.
    /// </summary>
    public static IQueryable<JobInstanceEntity> ApplyFilters(
        this IQueryable<JobInstanceEntity> queryable,
        JobInstanceQuery query)
    {
        // JobKeys/JobKey filtering (JobKeys takes priority)
        if (query.JobKeys is { Count: > 0 })
        {
            queryable = queryable.Where(i => query.JobKeys.Contains(i.JobKey));
        }
        else if (!string.IsNullOrEmpty(query.JobKey))
        {
            queryable = queryable.Where(i => i.JobKey == query.JobKey);
        }

        // JobKeyContains (using EF.Functions.Like for database-side LIKE)
        if (!string.IsNullOrEmpty(query.JobKeyContains))
        {
            queryable = queryable.Where(i => EF.Functions.Like(i.JobKey, $"%{query.JobKeyContains}%"));
        }

        // InstanceIdContains (using EF.Functions.Like)
        if (!string.IsNullOrEmpty(query.InstanceIdContains))
        {
            queryable = queryable.Where(i => EF.Functions.Like(i.InstanceId, $"%{query.InstanceIdContains}%"));
        }

        // States/State filtering (States takes priority)
        if (query.States is { Count: > 0 })
        {
            queryable = queryable.Where(i => query.States.Contains(i.State));
        }
        else if (query.State.HasValue)
        {
            queryable = queryable.Where(i => i.State == query.State.Value);
        }

        // Time range filtering
        if (query.CreatedAfter.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.CreatedBefore.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt <= query.CreatedBefore.Value);
        }

        return queryable;
    }

    /// <summary>
    /// Applies sorting based on SortBy and SortDescending properties.
    /// </summary>
    public static IQueryable<JobInstanceEntity> ApplySorting(
        this IQueryable<JobInstanceEntity> queryable,
        string? sortBy,
        bool descending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return descending
                ? queryable.OrderByDescending(i => i.CreatedAt)
                : queryable.OrderBy(i => i.CreatedAt);
        }

        return sortBy switch
        {
            "InstanceId" => descending
                ? queryable.OrderByDescending(i => i.InstanceId)
                : queryable.OrderBy(i => i.InstanceId),
            "JobKey" => descending
                ? queryable.OrderByDescending(i => i.JobKey)
                : queryable.OrderBy(i => i.JobKey),
            "State" => descending
                ? queryable.OrderByDescending(i => i.State)
                : queryable.OrderBy(i => i.State),
            "CreatedAt" => descending
                ? queryable.OrderByDescending(i => i.CreatedAt)
                : queryable.OrderBy(i => i.CreatedAt),
            "StartedAt" => descending
                ? queryable.OrderByDescending(i => i.StartedAt)
                : queryable.OrderBy(i => i.StartedAt),
            "CompletedAt" => descending
                ? queryable.OrderByDescending(i => i.CompletedAt)
                : queryable.OrderBy(i => i.CompletedAt),
            "Duration" => ApplyDurationSorting(queryable, descending),
            _ => descending
                ? queryable.OrderByDescending(i => i.CreatedAt)
                : queryable.OrderBy(i => i.CreatedAt)
        };
    }

    /// <summary>
    /// Applies duration-based sorting using database-agnostic expressions.
    /// Completed jobs are sorted by their actual duration, running jobs by elapsed time.
    /// </summary>
    private static IQueryable<JobInstanceEntity> ApplyDurationSorting(
        IQueryable<JobInstanceEntity> queryable,
        bool descending)
    {
        // EF Core translates this to provider-specific SQL
        // (TIMESTAMPDIFF for MySQL, DATEDIFF for SQL Server, etc.)
        if (descending)
        {
            return queryable.OrderByDescending(i =>
                i.StartedAt != null
                    ? ((i.CompletedAt ?? DateTime.UtcNow) - i.StartedAt.Value).TotalSeconds
                    : 0);
        }

        return queryable.OrderBy(i =>
            i.StartedAt != null
                ? ((i.CompletedAt ?? DateTime.UtcNow) - i.StartedAt.Value).TotalSeconds
                : 0);
    }

    /// <summary>
    /// Applies time range filter for state statistics queries.
    /// </summary>
    public static IQueryable<JobInstanceEntity> ApplyTimeRangeFilter(
        this IQueryable<JobInstanceEntity> queryable,
        DateTime? startTime,
        DateTime? endTime)
    {
        if (startTime.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            queryable = queryable.Where(i => i.CreatedAt <= endTime.Value);
        }

        return queryable;
    }

    /// <summary>
    /// Applies pagination and returns results.
    /// </summary>
    public static async Task<List<JobInstanceEntity>> ApplyPaginationAsync(
        this IQueryable<JobInstanceEntity> queryable,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await queryable
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
