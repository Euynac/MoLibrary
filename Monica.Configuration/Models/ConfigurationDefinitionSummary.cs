namespace Monica.Configuration.Models;

/// <summary>
/// Lightweight configuration definition summary for list views.
/// </summary>
public sealed record ConfigurationDefinitionSummary
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the binding section path.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the compact owning options type identity used for display and search.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the assembly name of the project that published this definition.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets a developer-defined category for grouping.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the schema hash used to detect drift across services.
    /// </summary>
    public required string SchemaHash { get; init; }

    /// <summary>
    /// Gets the last time the metadata store saw this definition.
    /// </summary>
    public DateTimeOffset? LastSeenTime { get; init; }

    /// <summary>
    /// Gets where this definition was resolved from for the current process.
    /// </summary>
    public ConfigurationDefinitionOrigin Origin { get; init; } = ConfigurationDefinitionOrigin.LocalScan;
}
