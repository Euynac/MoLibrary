namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persists one logical service's current reload-behavior contribution for a configuration definition.
/// </summary>
public sealed class ConfigurationDefinitionPublisherStateEntity
{
    /// <summary>
    /// Gets or sets the provider-independent, case-insensitive definition identity.
    /// </summary>
    public string DefinitionIdentity { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-independent, case-insensitive logical publisher identity.
    /// </summary>
    public string PublisherIdentity { get; set; } = "";

    /// <summary>
    /// Gets or sets the stable logical service key shared by replicas.
    /// </summary>
    public string PublisherKey { get; set; } = "";

    /// <summary>
    /// Gets or sets how the publisher obtained its contribution.
    /// </summary>
    public string ObservationKind { get; set; } = "";

    /// <summary>
    /// Gets or sets the publisher's normalized reload behavior.
    /// </summary>
    public string ReloadBehavior { get; set; } = "";
}
