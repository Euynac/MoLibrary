namespace Monica.Configuration.Models;

/// <summary>
/// Represents one source's contribution to a source chain.
/// </summary>
public sealed record ConfigurationSourceValue
{
    /// <summary>
    /// Gets the source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the source display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the source kind.
    /// </summary>
    public ConfigurationSourceKind Kind { get; init; }

    /// <summary>
    /// Gets the source priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets whether the source has a value at this path.
    /// </summary>
    public bool HasValue { get; init; }

    /// <summary>
    /// Gets whether the value is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the display-safe value.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets the last modification time.
    /// </summary>
    public DateTimeOffset? LastModifiedTime { get; init; }
}
