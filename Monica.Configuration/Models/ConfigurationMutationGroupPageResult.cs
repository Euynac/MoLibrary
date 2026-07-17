namespace Monica.Configuration.Models;

/// <summary>
/// Contains one bounded page of persisted configuration mutation groups.
/// </summary>
public sealed record ConfigurationMutationGroupPageResult
{
    /// <summary>
    /// Gets the matching groups in deterministic newest-first order.
    /// </summary>
    public required IReadOnlyList<ConfigurationMutationGroup> Items { get; init; }

    /// <summary>
    /// Gets the exclusive cursor for the next page, or null when this is the final page.
    /// </summary>
    public ConfigurationMutationGroupCursor? NextCursor { get; init; }

    /// <summary>
    /// Gets whether another matching group exists after this page.
    /// </summary>
    public bool HasMore { get; init; }
}
