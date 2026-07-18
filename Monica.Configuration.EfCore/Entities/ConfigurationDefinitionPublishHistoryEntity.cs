namespace Monica.Configuration.EfCore.Entities;

/// <summary>
/// Persisted audit row for configuration schema publication.
/// </summary>
public sealed class ConfigurationDefinitionPublishHistoryEntity
{
    public string HistoryId { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-independent identity of the published definition.
    /// </summary>
    public string DefinitionIdentity { get; set; } = "";

    public string DefinitionKey { get; set; } = "";

    public string SectionPath { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? Description { get; set; }

    public string FromProject { get; set; } = "";

    public string? Category { get; set; }

    public string ChangeKind { get; set; } = "";

    public int DefinitionRevision { get; set; }

    public int? PreviousSchemaVersion { get; set; }

    public int NewSchemaVersion { get; set; }

    public string? PreviousSchemaHash { get; set; }

    public string NewSchemaHash { get; set; } = "";

    public string? PreviousSchemaJson { get; set; }

    public string NewSchemaJson { get; set; } = "";

    public string ChangeSummaryJson { get; set; } = "";

    public string PublisherId { get; set; } = "";

    public string PublisherName { get; set; } = "";

    public string? PublisherVersion { get; set; }

    public DateTime PublishedTime { get; set; }
}
