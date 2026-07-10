namespace Monica.Configuration.Models;

/// <summary>
/// Describes a completed configuration mutation.
/// </summary>
public sealed record ConfigurationMutationResult
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the mutated logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the new value version.
    /// </summary>
    public long NewVersion { get; init; }

    /// <summary>
    /// Gets the schema version.
    /// </summary>
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the modification time.
    /// </summary>
    public DateTimeOffset ModifiedTime { get; init; }

    /// <summary>
    /// Gets whether the mutation requires process restart before it takes effect.
    /// </summary>
    public bool RequiresRestart { get; init; }

    /// <summary>
    /// Gets failures that occurred after this value was durably committed.
    /// </summary>
    public IReadOnlyList<ConfigurationPostCommitIssue> PostCommitIssues { get; init; } = [];
}
