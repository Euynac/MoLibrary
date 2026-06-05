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
    /// Gets the owning module.
    /// </summary>
    public string? OwnerModule { get; init; }

    /// <summary>
    /// Gets the schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets where this definition was resolved from for the current process.
    /// </summary>
    public ConfigurationDefinitionOrigin Origin { get; init; } = ConfigurationDefinitionOrigin.LocalScan;
}
