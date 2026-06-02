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
