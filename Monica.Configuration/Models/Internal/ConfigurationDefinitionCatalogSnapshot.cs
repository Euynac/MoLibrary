namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Internal resolver snapshot used to map diagnostic metadata through the Facade boundary.
/// </summary>
internal sealed record ConfigurationDefinitionCatalogSnapshot
{
    public required IReadOnlyList<ConfigurationDefinitionCatalogEntry> Entries { get; init; }

    public ConfigurationMetadataStoreDiagnostic? StoreDiagnostic { get; init; }
}

/// <summary>
/// Internal merged view of a local definition and its persisted metadata state.
/// </summary>
internal sealed record ConfigurationDefinitionCatalogEntry
{
    public ConfigurationDefinition? Definition { get; init; }

    public ConfigurationPublishedDefinitionRecord? PublishedMetadata { get; init; }

    public ConfigurationDefinitionMetadataDiagnostic? Diagnostic { get; init; }

    public ConfigurationDefinitionAvailability Availability { get; init; }

    public ConfigurationDefinitionLifecycleState LifecycleState { get; init; } =
        ConfigurationDefinitionLifecycleState.Active;
}
