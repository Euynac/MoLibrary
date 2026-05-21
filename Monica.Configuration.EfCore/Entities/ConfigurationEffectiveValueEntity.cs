namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted effective configuration document.
/// </summary>
public sealed class ConfigurationEffectiveValueEntity
{
    /// <summary>
    /// Gets or sets the owning configuration definition key.
    /// </summary>
    public string DefinitionKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the complete effective JSON document.
    /// </summary>
    public string Json { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the optimistic concurrency version.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the schema version used when the document was saved.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the last modification time.
    /// </summary>
    public DateTimeOffset LastModifiedTime { get; set; }

    /// <summary>
    /// Gets or sets the last modifier identity.
    /// </summary>
    public string? LastModifierId { get; set; }

    /// <summary>
    /// Gets or sets the last modifier display name.
    /// </summary>
    public string? LastModifierName { get; set; }
}
