namespace Monica.Configuration.Models;

/// <summary>
/// Describes the latest runtime operation state for one configuration store.
/// </summary>
public sealed record ConfigurationStoreState
{
    /// <summary>
    /// Gets the store key.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the latest operation time.
    /// </summary>
    public DateTimeOffset? LastOperationTime { get; init; }

    /// <summary>
    /// Gets whether the latest operation succeeded.
    /// </summary>
    public bool LastOperationSucceeded { get; init; } = true;

    /// <summary>
    /// Gets the latest operation error.
    /// </summary>
    public string? LastError { get; init; }
}
