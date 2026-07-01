namespace Monica.Configuration.Models;

/// <summary>
/// Summarizes a unified configuration version snapshot.
/// </summary>
public sealed record ConfigurationUnifiedVersionSummary
{
    /// <summary>
    /// Gets the global unified version number.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Gets the mutation group that triggered this version, if any.
    /// </summary>
    public string? MutationGroupId { get; init; }

    /// <summary>
    /// Gets the definition keys that triggered capture.
    /// </summary>
    public IReadOnlyList<string> TriggerDefinitionKeys { get; init; } = [];

    /// <summary>
    /// Gets all definition keys captured by the version.
    /// </summary>
    public IReadOnlyList<string> DefinitionKeys { get; init; } = [];

    /// <summary>
    /// Gets the number of captured definitions.
    /// </summary>
    public int DefinitionCount { get; init; }

    /// <summary>
    /// Gets when the version was created.
    /// </summary>
    public DateTimeOffset CreatedTime { get; init; }

    /// <summary>
    /// Gets the modifier identity.
    /// </summary>
    public string? ModifierId { get; init; }

    /// <summary>
    /// Gets the modifier display name.
    /// </summary>
    public string? ModifierName { get; init; }

    /// <summary>
    /// Gets the optional reason supplied by the operator.
    /// </summary>
    public string? Reason { get; init; }
}
