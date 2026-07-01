namespace Monica.Configuration.Models;

/// <summary>
/// Describes the difference between two unified configuration versions.
/// </summary>
public sealed record ConfigurationUnifiedVersionComparison
{
    /// <summary>
    /// Gets the origin version snapshot.
    /// </summary>
    public required ConfigurationUnifiedVersionSnapshot Origin { get; init; }

    /// <summary>
    /// Gets the target version snapshot.
    /// </summary>
    public required ConfigurationUnifiedVersionSnapshot Target { get; init; }

    /// <summary>
    /// Gets per-definition changes between the versions.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionDefinitionChange> Changes { get; init; } = [];
}

/// <summary>
/// Describes one definition's change between two unified configuration versions.
/// </summary>
public sealed record ConfigurationUnifiedVersionDefinitionChange
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name from the target version when possible.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the origin JSON payload, or null when the definition did not exist in the origin version.
    /// </summary>
    public string? OriginJson { get; init; }

    /// <summary>
    /// Gets the target JSON payload, or null when the definition did not exist in the target version.
    /// </summary>
    public string? TargetJson { get; init; }

    /// <summary>
    /// Gets the change kind.
    /// </summary>
    public ConfigurationUnifiedVersionDefinitionChangeKind ChangeKind { get; init; }
}

/// <summary>
/// Describes how a definition changed between unified versions.
/// </summary>
public enum ConfigurationUnifiedVersionDefinitionChangeKind
{
    /// <summary>
    /// The definition was added in the target version.
    /// </summary>
    Added,

    /// <summary>
    /// The definition was removed from the target version.
    /// </summary>
    Removed,

    /// <summary>
    /// The definition exists in both versions but its JSON changed.
    /// </summary>
    Modified
}
