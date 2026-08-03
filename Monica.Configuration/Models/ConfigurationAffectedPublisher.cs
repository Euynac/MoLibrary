namespace Monica.Configuration.Models;

/// <summary>
/// Describes one logical publishing service affected by a set of configuration definition changes.
/// </summary>
/// <remarks>Replicas that share a publisher key are represented by one logical publisher.</remarks>
public sealed record ConfigurationAffectedPublisher
{
    /// <summary>
    /// Gets the stable logical service key shared by replicas of the same publishing service.
    /// </summary>
    public required string PublisherKey { get; init; }

    /// <summary>
    /// Gets the changed definition keys currently consumed or possibly consumed by the service.
    /// </summary>
    public required IReadOnlyList<string> DefinitionKeys { get; init; }
}
