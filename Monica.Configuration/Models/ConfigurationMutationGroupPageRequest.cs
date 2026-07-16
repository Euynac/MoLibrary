namespace Monica.Configuration.Models;

/// <summary>
/// Defines a bounded query for persisted configuration mutation groups.
/// </summary>
public sealed record ConfigurationMutationGroupPageRequest
{
    /// <summary>
    /// Gets the maximum supported number of groups in one page.
    /// </summary>
    public const int MAX_PAGE_SIZE = 500;

    /// <summary>
    /// Gets the earliest group creation time to include.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Gets the latest group creation time to include.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Gets the optional definition key filter.
    /// </summary>
    public string? DefinitionKey { get; init; }

    /// <summary>
    /// Gets the exclusive continuation cursor returned by the preceding page, or null for the first page.
    /// </summary>
    public ConfigurationMutationGroupCursor? Cursor { get; init; }

    /// <summary>
    /// Gets the maximum number of groups to return.
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
            throw new ArgumentException("The mutation-group query start time cannot be later than its end time.");
        }
    }
}
