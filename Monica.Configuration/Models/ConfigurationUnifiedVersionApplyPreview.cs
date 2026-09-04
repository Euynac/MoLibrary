namespace Monica.Configuration.Models;

/// <summary>
/// Describes the exact configuration changes planned when rolling back to a unified version.
/// </summary>
public sealed record ConfigurationUnifiedVersionApplyPreview
{
    /// <summary>
    /// Gets the version that would be applied.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the fingerprint that binds an apply request to this reviewed preview.
    /// </summary>
    /// <remarks>
    /// Applying the rollback fails when current values, schemas, destinations, or concurrency tokens have changed
    /// since this fingerprint was created. Callers must request a new preview instead of applying stale changes.
    /// </remarks>
    public required string PreviewFingerprint { get; init; }

    /// <summary>
    /// Gets the per-definition apply targets, including definitions that require no change and definitions
    /// that will be skipped because they are unknown to the current process.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyTarget> Targets { get; init; } = [];

    /// <summary>
    /// Gets targets whose captured definitions are unknown to the current process and are skipped by the apply.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyTarget> SkippedTargets =>
        Targets.Where(static target => target.IsSkipped).ToArray();

    /// <summary>
    /// Gets the definition keys that are skipped because they are unknown to the current process.
    /// </summary>
    public IReadOnlyList<string> SkippedDefinitionKeys =>
        Targets.Where(static target => target.IsSkipped)
            .Select(static target => target.DefinitionKey)
            .ToArray();

    /// <summary>
    /// Gets the definitions that differ from a clean no-op rollback plan or require source reconciliation.
    /// Skipped definitions are excluded because they require no rollback write.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyTarget> ChangedTargets =>
        Targets.Where(static target => target.HasChanges).ToArray();

    /// <summary>
    /// Gets the number of definitions that require a rollback write, acknowledgement, or source reconciliation.
    /// </summary>
    public int ChangeCount => Targets.Count(static target => target.HasChanges);

    /// <summary>
    /// Gets the number of captured definitions that are unknown to the current process.
    /// </summary>
    public int SkippedCount => Targets.Count(static target => target.IsSkipped);

    /// <summary>
    /// Gets the number of path-level persistence mutations in the reviewed plan.
    /// </summary>
    public int MutationCount => Targets
        .Where(static target => !target.IsBlocked)
        .Sum(static target => target.Mutations.Count(static mutation =>
            mutation.Status == ConfigurationUnifiedVersionApplyMutationStatus.Ready));

    /// <summary>
    /// Gets the number of path or source findings that block the reviewed persistence plan.
    /// </summary>
    public int FindingCount => Targets
        .Where(static target => target.IsBlocked)
        .Sum(static target => target.Mutations.Count(static mutation =>
            mutation.Status != ConfigurationUnifiedVersionApplyMutationStatus.Ready));

    /// <summary>
    /// Gets the number of definitions already equal to the selected version.
    /// </summary>
    public int UnchangedCount => Targets.Count(static target => !target.HasChanges);

    /// <summary>
    /// Gets the number of changed definitions that require explicit acknowledgement of compatible schema drift.
    /// </summary>
    public int CompatibleSchemaDriftCount =>
        Targets.Count(static target => target.RequiresSchemaDriftAcknowledgement);

    /// <summary>
    /// Gets the number of changed definitions that cannot be applied safely.
    /// </summary>
    public int BlockedCount => Targets.Count(static target => target.IsBlocked);

    /// <summary>
    /// Gets whether the selected version requires at least one rollback write, acknowledgement, or reconciliation.
    /// </summary>
    public bool HasChanges => ChangeCount > 0;

    /// <summary>
    /// Gets whether one or more otherwise-compatible changes require schema-drift acknowledgement.
    /// </summary>
    public bool RequiresSchemaDriftAcknowledgement => CompatibleSchemaDriftCount > 0;

    /// <summary>
    /// Gets whether the plan can be applied without schema-drift acknowledgement.
    /// </summary>
    public bool CanApply => CanApplyWithAcknowledgement(acknowledgeCompatibleSchemaDrift: false);

    /// <summary>
    /// Gets whether the plan can be applied after acknowledging compatible schema drift.
    /// </summary>
    public bool CanApplyWithSchemaDriftAcknowledgement =>
        CanApplyWithAcknowledgement(acknowledgeCompatibleSchemaDrift: true);

    /// <summary>
    /// Determines whether the reviewed plan can be applied with the requested schema-drift acknowledgement.
    /// </summary>
    /// <param name="acknowledgeCompatibleSchemaDrift">
    /// True to allow values that validate against the current schema even though their captured schema hash differs.
    /// This never permits structurally invalid values.
    /// </param>
    /// <returns>
    /// True when the plan contains changes and every non-skipped changed target can be applied safely.
    /// Definitions unknown to the current process are skipped and reported instead of blocking the apply.
    /// </returns>
    public bool CanApplyWithAcknowledgement(bool acknowledgeCompatibleSchemaDrift)
    {
        return HasChanges && Targets
            .Where(static target => !target.IsSkipped)
            .All(target => target.CanApply(acknowledgeCompatibleSchemaDrift));
    }
}

/// <summary>
/// Describes one definition-level effective-value change and its path-level persistence plan.
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
    /// Gets the current effective JSON value, or null when the current definition cannot be resolved.
    /// </summary>
    public string? CurrentJson { get; init; }

    /// <summary>
    /// Gets the captured JSON value that would replace the current effective value.
    /// </summary>
    public required string TargetJson { get; init; }

    /// <summary>
    /// Gets the schema hash captured with the selected unified version.
    /// </summary>
    public required string CapturedSchemaHash { get; init; }

    /// <summary>
    /// Gets the currently resolved schema hash, or null when the definition no longer exists.
    /// </summary>
    public string? CurrentSchemaHash { get; init; }

    /// <summary>
    /// Gets the current schema version observed while the rollback preview was built.
    /// </summary>
    /// <remarks>
    /// The apply operation binds to this version together with <see cref="CurrentSchemaHash"/> so a schema
    /// replacement cannot be accepted without a new operator review, even when the numeric version is reused.
    /// </remarks>
    public int? CurrentSchemaVersion { get; init; }

    /// <summary>
    /// Gets the exact path-level writes required to produce the reviewed effective-value change.
    /// </summary>
    /// <remarks>
    /// The rollback never replaces an entire definition merely because one descendant changed. Each entry records
    /// the physical destination and concurrency token that will be used for that logical path.
    /// </remarks>
    public IReadOnlyList<ConfigurationUnifiedVersionApplyMutation> Mutations { get; init; } = [];

    /// <summary>
    /// Gets the apply target status.
    /// </summary>
    public ConfigurationUnifiedVersionApplyTargetStatus Status { get; init; } =
        ConfigurationUnifiedVersionApplyTargetStatus.Ready;

    /// <summary>
    /// Gets current-schema validation issues for the captured value.
    /// </summary>
    public IReadOnlyList<ConfigurationUnifiedVersionValidationIssue> ValidationIssues { get; init; } = [];

    /// <summary>
    /// Gets whether this target is skipped because its definition is unknown to the current process.
    /// </summary>
    /// <remarks>
    /// Historical version snapshots legitimately outlive the definitions they captured. A skipped target is
    /// excluded from command building and apply decisions, and is reported explicitly instead.
    /// </remarks>
    public bool IsSkipped => Status == ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition;

    /// <summary>
    /// Gets whether this target requires a rollback write, acknowledgement, or source reconciliation.
    /// </summary>
    public bool HasChanges =>
        Status != ConfigurationUnifiedVersionApplyTargetStatus.Unchanged && !IsSkipped;

    /// <summary>
    /// Gets whether this target requires explicit acknowledgement of compatible schema drift.
    /// </summary>
    public bool RequiresSchemaDriftAcknowledgement =>
        Status == ConfigurationUnifiedVersionApplyTargetStatus.CompatibleSchemaDrift;

    /// <summary>
    /// Gets whether this target is unsafe or impossible to apply.
    /// </summary>
    /// <remarks>Skipped targets are not blocked; they are neutral for the apply decision.</remarks>
    public bool IsBlocked => Status is
        ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue
        or ConfigurationUnifiedVersionApplyTargetStatus.RuntimeOutOfSync
        or ConfigurationUnifiedVersionApplyTargetStatus.ReadOnlyOverride
        or ConfigurationUnifiedVersionApplyTargetStatus.CompositeSourceConflict
        or ConfigurationUnifiedVersionApplyTargetStatus.LowerPriorityFallback
        or ConfigurationUnifiedVersionApplyTargetStatus.UnsupportedSource;

    /// <summary>
    /// Determines whether this target can participate in an apply operation.
    /// </summary>
    /// <param name="acknowledgeCompatibleSchemaDrift">Whether compatible schema drift was explicitly acknowledged.</param>
    /// <returns>True for no-op targets and for changed targets that satisfy the requested safety policy.</returns>
    public bool CanApply(bool acknowledgeCompatibleSchemaDrift)
    {
        return Status switch
        {
            ConfigurationUnifiedVersionApplyTargetStatus.Unchanged or
                ConfigurationUnifiedVersionApplyTargetStatus.Ready => true,
            ConfigurationUnifiedVersionApplyTargetStatus.CompatibleSchemaDrift =>
                acknowledgeCompatibleSchemaDrift,
            _ => false
        };
    }
}

/// <summary>
/// Describes one reviewed path-level write in a unified-version rollback plan.
/// </summary>
public sealed record ConfigurationUnifiedVersionApplyMutation
{
    /// <summary>
    /// Gets the canonical logical path changed by this mutation.
    /// </summary>
    public required string LogicalPath { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path written in the physical destination.
    /// </summary>
    public string? ConfigurationPath { get; init; }

    /// <summary>
    /// Gets whether the destination value is set or removed.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the current JSON observed for this path when it is safe to expose in the plan. This is normally the
    /// effective value and can be the durable source value for a runtime/source mismatch.
    /// </summary>
    public string? CurrentJson { get; init; }

    /// <summary>
    /// Gets the JSON value written by a set mutation, or null for a remove mutation.
    /// </summary>
    public string? TargetJson { get; init; }

    /// <summary>
    /// Gets the physical target source key when a writable destination was resolved.
    /// </summary>
    public string? SourceKey { get; init; }

    /// <summary>
    /// Gets the operator-facing physical target source name.
    /// </summary>
    public string? SourceDisplayName { get; init; }

    /// <summary>
    /// Gets the physical target source kind.
    /// </summary>
    public ConfigurationSourceKind? SourceKind { get; init; }

    /// <summary>
    /// Gets the source that makes the requested effective value unsafe or impossible, when applicable.
    /// </summary>
    public string? BlockingSourceDisplayName { get; init; }

    /// <summary>
    /// Gets the effective-store document version observed while the preview was built.
    /// </summary>
    /// <remarks>Version zero means that the document was confirmed absent.</remarks>
    public long? ExpectedValueVersion { get; init; }

    /// <summary>
    /// Gets the external-source revision observed while the preview was built.
    /// </summary>
    public string? ExpectedSourceRevision { get; init; }

    /// <summary>
    /// Gets the reviewed provider-chain revision for this logical path.
    /// </summary>
    public string? ExpectedSourceChainRevision { get; init; }

    /// <summary>
    /// Gets the destination-resolution status for this path.
    /// </summary>
    public ConfigurationUnifiedVersionApplyMutationStatus Status { get; init; } =
        ConfigurationUnifiedVersionApplyMutationStatus.Ready;
}

/// <summary>
/// Describes whether one path-level rollback mutation has a safe persistence destination.
/// </summary>
public enum ConfigurationUnifiedVersionApplyMutationStatus
{
    /// <summary>
    /// The path can be written to the resolved destination.
    /// </summary>
    Ready,

    /// <summary>
    /// The physical source changed without a corresponding runtime reload, so effective and durable state disagree.
    /// </summary>
    RuntimeOutOfSync,

    /// <summary>
    /// The effective source is read-only, so a write cannot change the effective value.
    /// </summary>
    ReadOnlyOverride,

    /// <summary>
    /// The changed container is composed from multiple providers and cannot be replaced in one source without
    /// creating unreviewed overrides.
    /// </summary>
    CompositeSourceConflict,

    /// <summary>
    /// Removing the effective value would reveal a lower-priority value instead of producing the reviewed target.
    /// </summary>
    LowerPriorityFallback,

    /// <summary>
    /// The effective source technology cannot be safely written by Monica.Configuration.
    /// </summary>
    UnsupportedSource
}

/// <summary>
/// Describes one incompatibility between a captured value and the current configuration schema.
/// </summary>
public sealed record ConfigurationUnifiedVersionValidationIssue
{
    /// <summary>
    /// Gets the canonical logical path of the incompatible value.
    /// </summary>
    public required string LogicalPath { get; init; }

    /// <summary>
    /// Gets the developer-facing validation detail.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the current schema validation rules associated with the incompatible value.
    /// </summary>
    /// <remarks>
    /// Consumers can use this structured metadata to present localized validation guidance without parsing the
    /// developer-facing <see cref="Message"/> text.
    /// </remarks>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];

    /// <summary>
    /// Gets whether the path and developer detail were withheld because historical or current sensitivity metadata
    /// cannot prove that they are safe to disclose.
    /// </summary>
    public bool DetailsHidden { get; init; }
}

/// <summary>
/// Describes whether one captured definition can be applied.
/// </summary>
public enum ConfigurationUnifiedVersionApplyTargetStatus
{
    /// <summary>
    /// The current effective value already equals the captured value, so no mutation is required.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The captured value matches the current schema and can be written to the resolved source.
    /// </summary>
    Ready,

    /// <summary>
    /// The schema hash changed, but the captured value remains valid under the current schema.
    /// Explicit acknowledgement is required because the exact historical schema is no longer active.
    /// </summary>
    CompatibleSchemaDrift,

    /// <summary>
    /// The captured value is structurally or semantically invalid under the current schema.
    /// </summary>
    InvalidValue,

    /// <summary>
    /// The current process no longer knows this definition. The target is skipped and reported;
    /// it does not block the rollback of the remaining definitions.
    /// </summary>
    MissingDefinition,

    /// <summary>
    /// The current runtime value and its physical source disagree, so rollback must wait for reconciliation.
    /// </summary>
    RuntimeOutOfSync,

    /// <summary>
    /// A higher-priority read-only source would still override the restored value.
    /// </summary>
    ReadOnlyOverride,

    /// <summary>
    /// A changed container is composed from multiple providers and has no single safe persistence destination.
    /// </summary>
    CompositeSourceConflict,

    /// <summary>
    /// Removing a value would expose a lower-priority source value rather than the reviewed target state.
    /// </summary>
    LowerPriorityFallback,

    /// <summary>
    /// The resolved source type cannot be written by Monica.Configuration.
    /// </summary>
    UnsupportedSource
}
