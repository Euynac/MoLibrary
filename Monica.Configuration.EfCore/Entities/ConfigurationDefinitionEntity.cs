namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted configuration schema definition.
/// </summary>
public sealed class ConfigurationDefinitionEntity
{
    public string DefinitionKey { get; set; } = "";

    public string SectionPath { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string ClrTypeName { get; set; } = "";

    public string FromProject { get; set; } = "";

    public string? Category { get; set; }

    public int SchemaVersion { get; set; }

    public string SchemaHash { get; set; } = "";

    public string ReloadBehavior { get; set; } = "";

    public string SchemaJson { get; set; } = "";

    public int PublishRevision { get; set; }
}
