namespace Monica.Configuration.Models;

/// <summary>
/// Describes one logical service's current contribution to a published definition's effective reload behavior.
/// </summary>
public sealed record ConfigurationDefinitionPublisherState
{
    /// <summary>
    /// Gets the stable logical service key shared by replicas of the same publishing service.
    /// </summary>
    public required string PublisherKey { get; init; }

    /// <summary>
    /// Gets how the publishing service obtained its reload-behavior evidence.
    /// </summary>
    public ConfigurationReloadBehaviorObservationKind ObservationKind { get; init; }

    /// <summary>
    /// Gets the normalized reload behavior reported by the publishing service.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }
}
