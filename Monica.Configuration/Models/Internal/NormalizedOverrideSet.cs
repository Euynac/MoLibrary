namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Represents a normalized set of overrides grouped by source.
/// </summary>
internal sealed record NormalizedOverrideSet
{
    /// <summary>
    /// Gets the source descriptor.
    /// </summary>
    public required ConfigurationSourceDescriptor Source { get; init; }

    /// <summary>
    /// Gets the normalized overrides from the source.
    /// </summary>
    public required IReadOnlyList<ConfigurationValueOverride> Overrides { get; init; }
}
