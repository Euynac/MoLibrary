namespace Monica.ProjectUnits.Annotations;

/// <summary>
/// Declares human-readable context for a project unit.
/// </summary>
/// <remarks>
/// The annotation is intentionally not inherited: every discovered project unit must describe its own architectural
/// responsibility. Missing metadata is reported as catalog coverage debt rather than as a discovery failure.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProjectUnitMetadataAttribute(string title) : Attribute
{
    /// <summary>
    /// Gets the concise display title used by diagnostics, logs, and management UIs.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// Gets or sets the team, role, or capability responsible for this unit.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Gets or sets a short explanation of the unit's business or architectural responsibility.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets searchable architectural or business classification tags.
    /// </summary>
    public string[] Tags { get; set; } = [];
}
