namespace Monica.Configuration.Models;

/// <summary>
/// Describes whether a unified version can be applied to the current configuration sources.
/// </summary>
public sealed record ConfigurationUnifiedVersionApplyPreview
{
    /// <summary>
    /// Gets the version that would be applied.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the per-definition apply targets.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyTarget> Targets { get; init; } = [];

    /// <summary>
    /// Gets whether every captured definition can be applied.
    /// </summary>
    public bool CanApply => Targets.Count > 0 && Targets.All(static target => target.Status == ConfigurationUnifiedVersionApplyTargetStatus.Ready);
}

/// <summary>
/// Describes where one captured definition would be written during rollback.
/// </summary>
public sealed record ConfigurationUnifiedVersionApplyTarget
{
    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target definition display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the target source key, when one was resolved.
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// Gets the target source display name.
    /// </summary>
    public string? SourceDisplayName { get; init; }

    /// <summary>
    /// Gets the target source kind.
    /// </summary>
    public ConfigurationSourceKind? SourceKind { get; init; }

    /// <summary>
    /// Gets the apply target status.
    /// </summary>
    public ConfigurationUnifiedVersionApplyTargetStatus Status { get; init; } = ConfigurationUnifiedVersionApplyTargetStatus.Ready;

    /// <summary>
    /// Gets an operator-facing diagnostic when the target cannot be applied.
    /// </summary>
    public string? Diagnostic { get; init; }
}

/// <summary>
/// Describes whether one captured definition can be applied.
/// </summary>
public enum ConfigurationUnifiedVersionApplyTargetStatus
{
    /// <summary>
    /// The captured definition can be written to the resolved source.
    /// </summary>
    Ready,

    /// <summary>
    /// The current process no longer knows this definition.
    /// </summary>
    MissingDefinition,

    /// <summary>
    /// The captured schema differs from the current definition schema.
    /// </summary>
    SchemaMismatch,

    /// <summary>
    /// A higher-priority read-only source would still override the restored value.
    /// </summary>
    ReadOnlyOverride,

    /// <summary>
    /// The resolved source type cannot be written by Monica.Configuration.
    /// </summary>
    UnsupportedSource
}
