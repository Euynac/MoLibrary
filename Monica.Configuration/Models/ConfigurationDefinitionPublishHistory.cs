namespace Monica.Configuration.Models;

/// <summary>
/// Records one persisted configuration definition publish event.
/// </summary>
public sealed record ConfigurationDefinitionPublishHistory
{
    /// <summary>
    /// Gets the stable history identifier.
    /// </summary>
    public required string HistoryId { get; init; }

    /// <summary>
    /// Gets the published definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the section path captured at publish time.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the display name captured at publish time.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the developer-facing description captured at publish time.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the assembly name of the project that published this definition.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the developer-defined category captured at publish time.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets why the publish history was recorded.
    /// </summary>
    public ConfigurationDefinitionPublishChangeKind ChangeKind { get; init; }

    /// <summary>
    /// Gets the definition revision produced by this publication.
    /// </summary>
    public int DefinitionRevision { get; init; }

    /// <summary>
    /// Gets the previous schema version, or null when this is the initial publish.
    /// </summary>
    public int? PreviousSchemaVersion { get; init; }

    /// <summary>
    /// Gets the new schema version after this publish.
    /// </summary>
    public int NewSchemaVersion { get; init; }

    /// <summary>
    /// Gets the previous schema hash, or null when this is the initial publish.
    /// </summary>
    public string? PreviousSchemaHash { get; init; }

    /// <summary>
    /// Gets the new schema hash after this publish.
    /// </summary>
    public required string NewSchemaHash { get; init; }

    /// <summary>
    /// Gets the previous compact schema JSON, or null when this is the initial publish.
    /// </summary>
    public string? PreviousSchemaJson { get; init; }

    /// <summary>
    /// Gets the new compact schema JSON after this publish.
    /// </summary>
    public required string NewSchemaJson { get; init; }

    /// <summary>
    /// Gets a compact structured summary of metadata and schema hash changes.
    /// </summary>
    public required string ChangeSummaryJson { get; init; }

    /// <summary>
    /// Gets the publishing process identifier.
    /// </summary>
    public required string PublisherId { get; init; }

    /// <summary>
    /// Gets the publishing host or pod name.
    /// </summary>
    public required string PublisherName { get; init; }

    /// <summary>
    /// Gets the publishing application version when available.
    /// </summary>
    public string? PublisherVersion { get; init; }

    /// <summary>
    /// Gets when this publish event was recorded.
    /// </summary>
    public DateTimeOffset PublishedTime { get; init; }
}
