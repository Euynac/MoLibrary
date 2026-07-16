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
    /// Gets effective values for scalar nodes and scalar collection nodes in the definition.
    /// </summary>
    public IReadOnlyList<ConfigurationEffectiveValue> EffectiveValues { get; init; } = [];
}
