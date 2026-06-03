namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted mutation history row.
/// </summary>
public sealed class ConfigurationValueHistoryEntity
{
    public string HistoryId { get; set; } = "";

    public string DefinitionKey { get; set; } = "";

    public string LogicalPath { get; set; } = "";

    public int PathDepth { get; set; }

    public string? ConfigurationPath { get; set; }

    public string TargetKind { get; set; } = "";

    public string? SourceProviderType { get; set; }

    public string? SourceDisplayName { get; set; }

    public string? SourcePhysicalPath { get; set; }

    public string? SourceConfigurationPath { get; set; }

    public string MutationKind { get; set; } = "";

    public string Granularity { get; set; } = "";

    public string State { get; set; } = "";

    public string? OldValueJson { get; set; }

    public string? NewValueJson { get; set; }

    public long Version { get; set; }

    public string? SourceRevisionBefore { get; set; }

    public string? SourceRevisionAfter { get; set; }

    public int SchemaVersion { get; set; }

    public DateTimeOffset ModifiedTime { get; set; }

    public string? ModifierId { get; set; }

    public string? ModifierName { get; set; }

    public string? Reason { get; set; }

    public string? MutationGroupId { get; set; }
}
