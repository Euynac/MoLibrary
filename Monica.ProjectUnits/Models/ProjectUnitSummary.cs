using System.Text.Json.Serialization;

namespace Monica.ProjectUnits.Models;

/// <summary>
/// Identifies another project unit without exposing its runtime reflection model.
/// </summary>
public sealed class ProjectUnitReference
{
    /// <summary>
    /// Gets the represented CLR type's full name.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the normalized display title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the architectural unit category.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; init; }
}

/// <summary>
/// Provides the serializable catalog projection used by APIs and management UIs.
/// </summary>
public sealed class ProjectUnitSummary
{
    /// <summary>
    /// Gets the represented CLR type's full name.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the normalized metadata title or the CLR type name fallback.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the effective description from metadata or XML documentation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the normalized owner declared in metadata.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// Gets normalized searchable metadata tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the architectural unit category.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EProjectUnitType UnitType { get; init; }

    /// <summary>
    /// Gets the stable execution points supported by Monica adapters for this unit type. Presence indicates a
    /// supported boundary and does not guarantee that every instance emits it.
    /// </summary>
    public IReadOnlyList<string> ExecutionPoints { get; init; } = [];

    /// <summary>
    /// Gets whether the represented type declares project-unit metadata explicitly.
    /// </summary>
    public bool HasExplicitMetadata { get; init; }

    /// <summary>
    /// Gets the number of normalized requirement references.
    /// </summary>
    public int RequirementCount { get; init; }

    /// <summary>
    /// Gets the number of public methods recorded for this unit.
    /// </summary>
    public int MethodCount { get; init; }

    /// <summary>
    /// Gets the project units on which this unit depends.
    /// </summary>
    public IReadOnlyList<ProjectUnitReference> Dependencies { get; init; } = [];

    /// <summary>
    /// Gets the number of units that depend on this unit.
    /// </summary>
    public int DependedByCount { get; init; }

    /// <summary>
    /// Gets catalog warnings and architecture alerts for this unit.
    /// </summary>
    public IReadOnlyList<ProjectUnitAlert> Alerts { get; init; } = [];
}
