namespace Monica.Configuration.Models;

/// <summary>
/// Describes a completed unified version rollback operation.
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
}
