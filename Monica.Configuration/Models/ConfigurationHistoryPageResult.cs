namespace Monica.Configuration.Models;

/// <summary>
/// Contains one bounded page of configuration mutation history.
/// </summary>
public sealed record ConfigurationHistoryPageResult
{
    /// <summary>
    /// Gets the matching physical history rows in deterministic newest-first order.
    /// </summary>
    public required IReadOnlyList<ConfigurationValueHistory> Items { get; init; }

    /// <summary>
    /// Gets the exclusive cursor for the next page, or null when this is the final page.
    /// </summary>
    public ConfigurationHistoryCursor? NextCursor { get; init; }

    /// <summary>
    /// Gets whether another matching mutation unit exists after this page.
    /// </summary>
    public bool HasMore { get; init; }
}
