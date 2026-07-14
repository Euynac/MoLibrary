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
}
