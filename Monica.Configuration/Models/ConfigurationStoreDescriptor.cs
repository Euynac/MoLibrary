namespace Monica.Configuration.Models;

/// <summary>
/// Describes one registered Monica.Configuration storage implementation.
/// </summary>
public sealed record ConfigurationStoreDescriptor
{
    /// <summary>
    /// Gets the stable store key.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the operator-facing display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the storage technology kind.
    /// </summary>
    public ConfigurationStoreKind Kind { get; init; }

    /// <summary>
    /// Gets whether the store accepts writes.
    /// </summary>
    public bool IsWritable { get; init; } = true;

    /// <summary>
    /// Gets whether this store persists current effective values.
    /// </summary>
    public bool SupportsEffectiveValues { get; init; }

    /// <summary>
    /// Gets whether this store persists mutation history.
    /// </summary>
    public bool SupportsHistory { get; init; }

    /// <summary>
    /// Gets whether this store persists definition metadata.
    /// </summary>
    public bool SupportsMetadata { get; init; }

    /// <summary>
    /// Gets whether this store can notify other processes about changes.
    /// </summary>
    public bool SupportsNotifications { get; init; }
}
