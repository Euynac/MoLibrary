namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted configuration override row.
/// </summary>
public sealed class ConfigurationValueOverrideEntity
{
    public string OverrideId { get; set; } = "";

    public string DefinitionKey { get; set; } = "";

    public string LogicalPath { get; set; } = "";

    public int PathDepth { get; set; }

    public string? ConfigurationPath { get; set; }

    public string SourceKey { get; set; } = "";

    public string Granularity { get; set; } = "";

    public string State { get; set; } = "";

    public string StoredValueKind { get; set; } = "";

    public string? PlainJson { get; set; }

    public string? ProtectedPayload { get; set; }

    public string? SecretReference { get; set; }

    public long Version { get; set; }

    public int SchemaVersion { get; set; }

    public DateTimeOffset LastModifiedTime { get; set; }

    public string? LastModifierId { get; set; }

    public string? LastModifierName { get; set; }
}
