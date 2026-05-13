namespace Monica.Configuration.Models;

/// <summary>
/// Defines a Monica-managed configuration aggregate rooted at an options type.
/// </summary>
public sealed record ConfigurationDefinition
{
    /// <summary>
    /// Gets the stable globally unique definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the Microsoft configuration section used for options binding.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the display name shown in management tools.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the assembly-qualified CLR type name of the owning options type.
    /// </summary>
    public required string ClrTypeName { get; init; }

    /// <summary>
    /// Gets the owning module or application component.
    /// </summary>
    public string? OwnerModule { get; init; }

    /// <summary>
    /// Gets a developer-defined category for grouping.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the schema version published by the owner.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets the schema hash used to detect drift across services.
    /// </summary>
    public required string SchemaHash { get; init; }

    /// <summary>
    /// Gets the default reload behavior for the definition.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }

    /// <summary>
    /// Gets the root node of the configuration schema tree.
    /// </summary>
    public required ConfigurationNodeDefinition Root { get; init; }
}
