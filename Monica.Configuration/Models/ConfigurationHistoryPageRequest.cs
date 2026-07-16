namespace Monica.Configuration.Models;

/// <summary>
/// Defines a bounded query for configuration mutation history.
/// </summary>
/// <remarks>
/// Pagination counts mutation units rather than physical history rows. All matching rows that share a mutation-group
/// identity belong to one unit, so a persisted mutation group is never split across page boundaries.
/// </remarks>
public sealed record ConfigurationHistoryPageRequest
{
    /// <summary>
    /// Gets the maximum supported number of mutation units in one page.
    /// </summary>
    public const int MAX_PAGE_SIZE = 500;

    /// <summary>
    /// Gets the earliest modification time to include.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Gets the latest modification time to include.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Gets the optional definition key filter.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets the optional exact logical-path filter.
    /// </summary>
    public LogicalPath? LogicalPath { get; init; }

    /// <summary>
    /// Gets the optional mutation-group filter.
    /// </summary>
    public string? MutationGroupId { get; init; }

    /// <summary>
    /// Gets the optional persistence-target filter.
    /// </summary>
    public ConfigurationMutationTargetKind? TargetKind { get; init; }

    /// <summary>
    /// Gets the exclusive continuation cursor returned by the preceding page, or null for the first page.
    /// </summary>
    public ConfigurationHistoryCursor? Cursor { get; init; }

    /// <summary>
    /// Gets the maximum number of mutation units to return. A unit may contain multiple physical history rows.
    /// </summary>
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Validates the requested range and pagination bounds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="PageSize"/> is outside the supported range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="From"/> is later than <see cref="To"/> or <see cref="Cursor"/> is invalid.
    /// </exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, MAX_PAGE_SIZE);
        Cursor?.Validate();

        if (From is { } from && To is { } to && from > to)
        {
            throw new ArgumentException("The history query start time cannot be later than its end time.");
        }
    }
}
