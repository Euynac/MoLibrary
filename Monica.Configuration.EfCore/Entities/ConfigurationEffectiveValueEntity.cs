using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted effective configuration document.
/// </summary>
public sealed class ConfigurationEffectiveValueEntity
{
    internal static ConfigurationEffectiveValueEntity Create(string definitionKey)
    {
        return new ConfigurationEffectiveValueEntity
        {
            DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(definitionKey),
            DefinitionKey = definitionKey
        };
    }

    /// <summary>
    /// Gets or sets the provider-independent, case-insensitive definition identity.
    /// </summary>
    public string DefinitionIdentity { get; set; } = "";

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
    public DateTime LastModifiedTime { get; set; }

    /// <summary>
    /// Gets or sets the last modifier identity.
    /// </summary>
    public string? LastModifierId { get; set; }

    /// <summary>
    /// Gets or sets the last modifier display name.
    /// </summary>
    public string? LastModifierName { get; set; }

    internal void Apply(
        string normalizedJson,
        int schemaVersion,
        DateTime modifiedTime,
        string? modifierId,
        string? modifierName)
    {
        DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(DefinitionKey);
        Json = normalizedJson;
        Version++;
        SchemaVersion = schemaVersion;
        LastModifiedTime = modifiedTime;
        LastModifierId = modifierId;
        LastModifierName = modifierName;
    }
}
