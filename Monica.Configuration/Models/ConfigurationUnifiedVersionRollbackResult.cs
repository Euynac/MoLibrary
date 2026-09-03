namespace Monica.Configuration.Models;

/// <summary>
/// Describes the persisted outcome of a unified-version rollback operation.
/// The contained mutation group can be partially applied when independent persistence boundaries do not all commit.
/// </summary>
public sealed record ConfigurationUnifiedVersionRollbackResult
{
    /// <summary>
    /// Gets the version that was applied.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the mutation group created by the rollback.
    /// </summary>
    public required ConfigurationMutationGroup MutationGroup { get; init; }

    /// <summary>
    /// Gets the mutation results produced by applying captured definitions.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the structured group outcome, including partial external-source outcomes and post-commit issues.
    /// </summary>
    public required ConfigurationMutationGroupApplyResult ApplyResult { get; init; }

    /// <summary>
    /// Gets the captured definition keys that were skipped because they are unknown to the current process.
    /// </summary>
    /// <remarks>
    /// Historical snapshots legitimately outlive their definitions; skipped definitions were not restored and
    /// operators must be able to see that the rollback does not cover them.
    /// </remarks>
    public IReadOnlyList<string> SkippedDefinitionKeys { get; init; } = [];
}
