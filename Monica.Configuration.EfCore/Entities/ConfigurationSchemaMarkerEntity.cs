namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Tracks the physical schema version for Configuration-owned EF Core tables.
/// </summary>
internal sealed class ConfigurationSchemaMarkerEntity
{
    internal const string CurrentMarkerKey = "Configuration.EfCore";

    internal const int CurrentSchemaVersion = 2;

    public string MarkerKey { get; set; } = CurrentMarkerKey;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
}
