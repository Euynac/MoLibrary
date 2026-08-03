namespace Monica.Configuration.Models;

/// <summary>
/// Describes the logical publishing services affected by a set of configuration definition changes.
/// </summary>
/// <remarks>
/// This is a point-in-time view of publisher metadata, not a service-liveness or reload-delivery guarantee.
/// </remarks>
public sealed record ConfigurationDefinitionChangeImpact
{
    /// <summary>
    /// Gets the normalized, distinct definition keys included in the impact analysis.
    /// </summary>
    public required IReadOnlyList<string> DefinitionKeys { get; init; }

    /// <summary>
    /// Gets participating logical publishers and the changed definitions associated with each publisher.
    /// </summary>
    public required IReadOnlyList<ConfigurationAffectedPublisher> AffectedPublishers { get; init; }

    /// <summary>
    /// Gets definition keys for which no current publisher reports consumption or possible consumption.
    /// </summary>
    public required IReadOnlyList<string> DefinitionsWithoutKnownConsumers { get; init; }
}
