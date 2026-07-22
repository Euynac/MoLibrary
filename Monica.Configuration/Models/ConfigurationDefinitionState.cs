namespace Monica.Configuration.Models;

/// <summary>
/// Provides one configuration definition together with the display-safe effective values needed by management views.
/// </summary>
public sealed record ConfigurationDefinitionState
{
    /// <summary>
    /// Gets the complete definition that describes the returned values.
    /// </summary>
    public required ConfigurationDefinition Definition { get; init; }

    /// <summary>
    /// Gets the persisted effective-value document version observed while building this state, or null when no
    /// persisted document exists.
    /// </summary>
    public long? EffectiveValueVersion { get; init; }

    /// <summary>
    /// Gets display-safe effective values for every statically declared non-root schema node in preorder.
    /// Aggregate values whose schema subtree contains sensitive data are redacted as a whole.
    /// </summary>
    public IReadOnlyList<ConfigurationEffectiveValue> EffectiveValues { get; init; } = [];
}
