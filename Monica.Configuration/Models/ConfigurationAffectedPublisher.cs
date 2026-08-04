namespace Monica.Configuration.Models;

/// <summary>
/// Describes one logical publishing service associated with a set of changed configuration parameters.
/// </summary>
/// <remarks>Replicas that share a publisher key are represented by one logical publisher.</remarks>
public sealed record ConfigurationAffectedPublisher
{
    /// <summary>
    /// Gets the stable logical service key shared by replicas of the same publishing service.
    /// </summary>
    public required string PublisherKey { get; init; }

    /// <summary>
    /// Gets changed parameters attributed to definitions currently consumed or possibly consumed by the service.
    /// </summary>
    public required IReadOnlyList<ConfigurationAffectedParameter> Parameters { get; init; }

    /// <summary>
    /// Gets whether at least one changed parameter requires a process restart for this service.
    /// </summary>
    public bool RequiresRestart => Parameters.Any(static parameter => parameter.RequiresRestart);
}
