namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted definition document inside a unified configuration version.
/// </summary>
public sealed class ConfigurationUnifiedVersionDocumentEntity
{
    public long Version { get; set; }

    public string DefinitionKey { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? Category { get; set; }

    public string FromProject { get; set; } = "";

    public int SchemaVersion { get; set; }

    public string SchemaHash { get; set; } = "";

    public long? EffectiveValueVersion { get; set; }

    public string Json { get; set; } = "{}";

    public string SourceContributionsJson { get; set; } = "[]";
}
