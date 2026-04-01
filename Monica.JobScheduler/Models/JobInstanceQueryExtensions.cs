namespace Monica.JobScheduler.Models;

/// <summary>
/// Extension methods for applying JobInstanceQuery filters to IEnumerable{JobInstance}.
/// Used by InMemoryJobMetadataRepository.
/// </summary>
public static class JobInstanceQueryExtensions
{
    /// <summary>
    /// Applies all filters from JobInstanceQuery to the enumerable.
    /// </summary>
    public static IEnumerable<JobInstance> ApplyFilters(
        this IEnumerable<JobInstance> source,
        JobInstanceQuery query)
    {
        // JobKeys/JobKey filtering (JobKeys takes priority)
        if (query.JobKeys is { Count: > 0 })
        {
            source = source.Where(i => query.JobKeys.Contains(i.JobKey));
        }
        else if (!string.IsNullOrEmpty(query.JobKey))
        {
            source = source.Where(i => i.JobKey == query.JobKey);
        }

        // JobKeyContains (case-insensitive)
        if (!string.IsNullOrEmpty(query.JobKeyContains))
        {
            source = source.Where(i => i.JobKey.Contains(query.JobKeyContains, StringComparison.OrdinalIgnoreCase));
        }

        // InstanceIdContains (case-insensitive)
        if (!string.IsNullOrEmpty(query.InstanceIdContains))
        {
            source = source.Where(i => i.InstanceId.Contains(query.InstanceIdContains, StringComparison.OrdinalIgnoreCase));
        }

        // States/State filtering (States takes priority)
        if (query.States is { Count: > 0 })
        {
            source = source.Where(i => query.States.Contains(i.State));
        }
        else if (query.State.HasValue)
        {
            source = source.Where(i => i.State == query.State.Value);
        }

        // Time range filtering
        if (query.CreatedAfter.HasValue)
        {
            source = source.Where(i => i.CreatedAt >= query.CreatedAfter.Value);
        }

        if (query.CreatedBefore.HasValue)
        {
            source = source.Where(i => i.CreatedAt <= query.CreatedBefore.Value);
        }

        return source;
    }

    /// <summary>
    /// Applies sorting based on SortBy and SortDescending properties.
    /// </summary>
    public static IEnumerable<JobInstance> ApplySorting(
        this IEnumerable<JobInstance> source,
        string? sortBy,
        bool descending)
    {
        if (string.IsNullOrEmpty(sortBy))
        {
            return descending
                ? source.OrderByDescending(i => i.CreatedAt)
                : source.OrderBy(i => i.CreatedAt);
        }

        return sortBy switch
        {
            "InstanceId" => descending
                ? source.OrderByDescending(i => i.InstanceId)
                : source.OrderBy(i => i.InstanceId),
            "JobKey" => descending
                ? source.OrderByDescending(i => i.JobKey)
                : source.OrderBy(i => i.JobKey),
            "State" => descending
                ? source.OrderByDescending(i => i.State)
                : source.OrderBy(i => i.State),
            "CreatedAt" => descending
                ? source.OrderByDescending(i => i.CreatedAt)
                : source.OrderBy(i => i.CreatedAt),
            "StartedAt" => descending
                ? source.OrderByDescending(i => i.StartedAt ?? DateTime.MinValue)
                : source.OrderBy(i => i.StartedAt ?? DateTime.MinValue),
            "CompletedAt" => descending
                ? source.OrderByDescending(i => i.CompletedAt ?? DateTime.MinValue)
                : source.OrderBy(i => i.CompletedAt ?? DateTime.MinValue),
            "Duration" => ApplyDurationSorting(source, descending),
            _ => descending
                ? source.OrderByDescending(i => i.CreatedAt)
                : source.OrderBy(i => i.CreatedAt)
        };
    }

    /// <summary>
    /// Applies duration-based sorting.
    /// Completed jobs sorted by actual duration, running jobs by elapsed time.
    /// </summary>
    private static IEnumerable<JobInstance> ApplyDurationSorting(
        IEnumerable<JobInstance> source,
        bool descending)
    {
        return descending
            ? source.OrderByDescending(i =>
                i.CompletedAt.HasValue && i.StartedAt.HasValue
                    ? (i.CompletedAt.Value - i.StartedAt.Value).TotalSeconds
                    : (i.StartedAt.HasValue ? (DateTime.UtcNow - i.StartedAt.Value).TotalSeconds : 0))
            : source.OrderBy(i =>
                i.CompletedAt.HasValue && i.StartedAt.HasValue
                    ? (i.CompletedAt.Value - i.StartedAt.Value).TotalSeconds
                    : (i.StartedAt.HasValue ? (DateTime.UtcNow - i.StartedAt.Value).TotalSeconds : 0));
    }

    /// <summary>
    /// Applies time range filter for state statistics queries.
    /// </summary>
    public static IEnumerable<JobInstance> ApplyTimeRangeFilter(
        this IEnumerable<JobInstance> source,
        DateTime? startTime,
        DateTime? endTime)
    {
        if (startTime.HasValue)
        {
            source = source.Where(i => i.CreatedAt >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            source = source.Where(i => i.CreatedAt <= endTime.Value);
        }

        return source;
    }

    /// <summary>
    /// Applies pagination (skip/take).
    /// </summary>
    public static List<T> ApplyPagination<T>(
        this IEnumerable<T> source,
        int pageNumber,
        int pageSize)
    {
        return source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }
}
