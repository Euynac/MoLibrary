namespace Monica.Configuration.Models;

/// <summary>
/// Describes a Monica configuration value source.
/// </summary>
public sealed record ConfigurationSourceDescriptor
{
    /// <summary>
    /// Gets the stable source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the source kind.
    /// </summary>
    public ConfigurationSourceKind Kind { get; init; }

    /// <summary>
    /// Gets the merge priority. Larger values win.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets whether this source accepts mutations.
    /// </summary>
    public bool IsWritable { get; init; }

    /// <summary>
    /// Gets whether this source can watch its own backing store.
    /// </summary>
    public bool SupportsWatch { get; init; }

    /// <summary>
    /// Gets whether this source can provide history records.
    /// </summary>
    public bool SupportsHistory { get; init; }
}
