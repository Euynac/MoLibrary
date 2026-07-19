using Monica.Configuration.Models;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Represents one staged configuration mutation in the UI before it is persisted as part of a group.
/// </summary>
public sealed record PendingChange
{
    /// <summary>
    /// Gets the stable mutation request identity used to correlate save outcomes.
    /// </summary>
    public string RequestId => $"{DefinitionKey}|{LogicalPath.ToCanonicalString()}";

    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the display name of the target definition.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the target node display name.
    /// </summary>
    public required string NodeDisplayName { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the storage target that should receive this staged mutation.
    /// </summary>
    public ConfigurationMutationTargetKind TargetKind { get; init; } = ConfigurationMutationTargetKind.MonicaEffectiveStore;

    /// <summary>
    /// Gets the external source key when <see cref="TargetKind"/> targets a Microsoft configuration provider.
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// Gets the external source display name when applicable.
    /// </summary>
    public string? SourceDisplayName { get; init; }

    /// <summary>
    /// Gets the external source provider type when applicable.
    /// </summary>
    public string? SourceProviderType { get; init; }

    /// <summary>
    /// Gets the physical source path when the staged mutation targets a file-backed source.
    /// </summary>
    public string? SourcePhysicalPath { get; init; }

    /// <summary>
    /// Gets the source configuration path being edited when the staged mutation targets an external provider.
    /// </summary>
    public string? SourceConfigurationPath { get; init; }

    /// <summary>
    /// Gets the new stored payload.
    /// </summary>
    public required ConfigurationStoredValue NewValue { get; init; }

    /// <summary>
    /// Gets the previous stored payload when known.
    /// </summary>
    public ConfigurationStoredValue? OriginalValue { get; init; }

    /// <summary>
    /// Gets the display-safe previous value.
    /// </summary>
    public string? OriginalDisplayValue { get; init; }

    /// <summary>
    /// Gets the display-safe staged value.
    /// </summary>
    public string? NewDisplayValue { get; init; }

    /// <summary>
    /// Gets the expected schema version for validation.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the expected effective-document version for optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// Every Monica-store mutation for the same definition in one group must carry the same version. External-source
    /// mutations do not use this value.
    /// </remarks>
    public long? ExpectedValueVersion { get; init; }

    /// <summary>
    /// Gets whether the node contains sensitive data.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets the node kind.
    /// </summary>
    public ConfigurationNodeKind NodeKind { get; init; }

    /// <summary>
    /// Gets the scalar value kind when the target node is scalar.
    /// </summary>
    public ConfigurationValueKind? ValueKind { get; init; }

    /// <summary>
    /// Gets the effective reload behavior for the target node.
    /// </summary>
    public ConfigurationReloadBehavior ReloadBehavior { get; init; }
}
