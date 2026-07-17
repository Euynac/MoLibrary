namespace Monica.Configuration.Models;

/// <summary>
/// Combines the current publication state and revision history of one configuration definition.
/// </summary>
public sealed record ConfigurationDefinitionPublicationOverview
{
    /// <summary>
    /// Gets the stable definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the current definition revision, which advances for every effective canonical change.
    /// </summary>
    public int DefinitionRevision { get; init; }

    /// <summary>
    /// Gets the current structural schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the conservative reload behavior aggregated from current publisher states.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }

    /// <summary>
    /// Gets current logical-service contributions to the effective reload behavior.
    /// </summary>
    public required IReadOnlyList<ConfigurationDefinitionPublisherState> PublisherStates { get; init; }

    /// <summary>
    /// Gets newest definition revisions first.
    /// </summary>
    public required IReadOnlyList<ConfigurationDefinitionPublishHistory> RevisionHistories { get; init; }
}
