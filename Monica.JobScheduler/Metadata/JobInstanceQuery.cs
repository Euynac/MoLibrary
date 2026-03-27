using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Metadata;

/// <summary>
/// JobInstance query conditions
/// </summary>
public record JobInstanceQuery
{
    /// <summary>
    /// JobKey exact match
    /// </summary>
    public string? JobKey { get; init; }

    /// <summary>
    /// JobKey batch exact matching (priority is higher than JobKey attribute)
    /// </summary>
    public List<string>? JobKeys { get; init; }

    /// <summary>
    /// JobKey fuzzy matching (inclusive relationship, case-insensitive)
    /// </summary>
    public string? JobKeyContains { get; init; }

    /// <summary>
    /// InstanceId fuzzy matching (inclusive relationship, case-insensitive)
    /// </summary>
    public string? InstanceIdContains { get; init; }

    /// <summary>
    /// Filter by status (single status)
    /// </summary>
    public JobState? State { get; init; }

    /// <summary>
    /// Filter by multiple states (takes precedence over State property)
    /// </summary>
    public List<JobState>? States { get; init; }

    /// <summary>
    /// Creation time start (inclusive)
    /// </summary>
    public DateTime? CreatedAfter { get; init; }

    /// <summary>
    /// End of creation time (inclusive)
    /// </summary>
    public DateTime? CreatedBefore { get; init; }

    /// <summary>
    /// Sort directions by creation time
    /// </summary>
    public SortDirection SortByCreatedAt { get; init; } = SortDirection.Descending;

    /// <summary>
    /// Sorting field name (supported: InstanceId, JobKey, State, CreatedAt, StartedAt, CompletedAt, Duration)
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// Whether to sort in descending order
    /// </summary>
    public bool SortDescending { get; init; } = true;

    /// <summary>
    /// Page number (starting from 1)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// page size
    /// </summary>
    public int PageSize { get; init; } = 20;
}
