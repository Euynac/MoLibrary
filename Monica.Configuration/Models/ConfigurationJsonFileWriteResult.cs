namespace Monica.Configuration.Models;

/// <summary>
/// Describes a completed JSON file source write.
/// </summary>
public sealed record ConfigurationJsonFileWriteResult
{
    /// <summary>
    /// Gets the value before the write.
    /// </summary>
    public ConfigurationStoredValue? OldValue { get; init; }

    /// <summary>
    /// Gets the value after the write.
    /// </summary>
    public required ConfigurationStoredValue NewValue { get; init; }

    /// <summary>
    /// Gets the source revision before the write.
    /// </summary>
    public required string OldRevision { get; init; }

    /// <summary>
    /// Gets the source revision after the write.
    /// </summary>
    public required string NewRevision { get; init; }

    /// <summary>
    /// Gets the modification time.
    /// </summary>
    public DateTimeOffset ModifiedTime { get; init; }
}

/// <summary>
/// Describes one requested mutation inside an atomic JSON source write.
/// </summary>
public sealed record ConfigurationJsonFileMutation
{
    /// <summary>
    /// Gets the Microsoft configuration path.
    /// </summary>
    public required string ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the stored JSON value used by set mutations.
    /// </summary>
    public required ConfigurationStoredValue Value { get; init; }
}

/// <summary>
/// Describes one value outcome inside a JSON source batch write.
/// </summary>
public sealed record ConfigurationJsonFileMutationResult
{
    /// <summary>
    /// Gets the value before the write.
    /// </summary>
    public ConfigurationStoredValue? OldValue { get; init; }

    /// <summary>
    /// Gets the value after the write.
    /// </summary>
    public required ConfigurationStoredValue NewValue { get; init; }
}

/// <summary>
/// Describes a completed atomic write of one JSON configuration source.
/// </summary>
public sealed record ConfigurationJsonFileBatchWriteResult
{
    /// <summary>
    /// Gets per-mutation outcomes in submitted order.
    /// </summary>
    public IReadOnlyList<ConfigurationJsonFileMutationResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the source revision before the write.
    /// </summary>
    public required string OldRevision { get; init; }

    /// <summary>
    /// Gets the source revision after the write.
    /// </summary>
    public required string NewRevision { get; init; }

    /// <summary>
    /// Gets the shared modification time.
    /// </summary>
    public DateTimeOffset ModifiedTime { get; init; }
}
