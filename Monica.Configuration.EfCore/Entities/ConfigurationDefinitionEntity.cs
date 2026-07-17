namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted configuration schema definition.
/// </summary>
public sealed class ConfigurationDefinitionEntity
{
    /// <summary>
    /// Gets or sets the provider-independent, case-insensitive definition identity.
    /// </summary>
    public string DefinitionIdentity { get; set; } = "";

    public string DefinitionKey { get; set; } = "";

    public string SectionPath { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? Description { get; set; }

    public string ClrTypeName { get; set; } = "";

    public string FromProject { get; set; } = "";

    public string? Category { get; set; }

    public int SchemaVersion { get; set; }

    public string SchemaHash { get; set; } = "";

    public string ReloadBehavior { get; set; } = "";

    public string SchemaJson { get; set; } = "";

    public int DefinitionRevision { get; set; }
}
