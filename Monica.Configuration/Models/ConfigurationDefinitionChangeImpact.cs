namespace Monica.Configuration.Models;

/// <summary>
/// Describes the logical publishing services affected by a set of configuration parameter changes.
/// </summary>
/// <remarks>
/// This is a point-in-time view of publisher metadata, not a service-liveness or reload-delivery guarantee.
/// </remarks>
public sealed record ConfigurationDefinitionChangeImpact
{
    /// <summary>
    /// Gets participating logical publishers and the changed parameters associated with each publisher.
    /// </summary>
    public required IReadOnlyList<ConfigurationAffectedPublisher> AffectedPublishers { get; init; }

    /// <summary>
    /// Gets changed parameters for which no current publisher reports consumption or possible consumption.
    /// </summary>
    public required IReadOnlyList<ConfigurationParameterWithoutKnownConsumer> ParametersWithoutKnownConsumers { get; init; }

    /// <summary>
    /// Gets whether the reviewed change set should carry a restart advisory.
    /// </summary>
    /// <remarks>
    /// Parameters without a known consumer are treated conservatively because their runtime behavior cannot be
    /// established from current publisher metadata.
    /// </remarks>
    public bool RequiresRestart => AffectedPublishers.Any(static publisher => publisher.RequiresRestart)
                                   || ParametersWithoutKnownConsumers.Count != 0;
}
