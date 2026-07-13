namespace Monica.Configuration.Models;

/// <summary>
/// Describes a prepared mutation group that a store must persist as one commit boundary when supported.
/// </summary>
public sealed record ConfigurationMutationBatchCommitRequest
{
    /// <summary>
    /// Gets the mutation group row to persist.
    /// </summary>
    public required ConfigurationMutationGroup MutationGroup { get; init; }

    /// <summary>
    /// Gets prepared document and history steps in application order.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationBatchCommitItem> Items { get; init; } = [];

    /// <summary>
    /// Gets the unified-version snapshot to append in the same commit, when enabled.
    /// </summary>
    public ConfigurationUnifiedVersionCreateRequest? UnifiedVersion { get; init; }
}

/// <summary>
/// Couples one prepared document save with its matching history row.
/// </summary>
public sealed record ConfigurationMutationBatchCommitItem
{
    /// <summary>
    /// Gets the caller request identity.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the prepared document save.
    /// </summary>
    public required ConfigurationEffectiveValueSaveRequest SaveRequest { get; init; }

    /// <summary>
    /// Gets the matching history row.
    /// </summary>
    public required ConfigurationValueHistory History { get; init; }
}

/// <summary>
/// Describes a completed store-level mutation-group commit.
/// </summary>
public sealed record ConfigurationMutationBatchCommitResult
{
    /// <summary>
    /// Gets the persisted mutation group.
    /// </summary>
    public required ConfigurationMutationGroup MutationGroup { get; init; }

    /// <summary>
    /// Gets saved documents keyed by definition.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigurationEffectiveValueDocument> Documents { get; init; } =
        new Dictionary<string, ConfigurationEffectiveValueDocument>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets request identities whose effective values crossed the store persistence boundary.
    /// </summary>
    public IReadOnlyList<string> AppliedRequestIds { get; init; } = [];

    /// <summary>
    /// Gets the first persistence failure for a best-effort store, when one occurred.
    /// </summary>
    public ConfigurationMutationBatchFailure? Failure { get; init; }

    /// <summary>
    /// Gets non-blocking audit or version-capture failures that happened after value persistence.
    /// </summary>
    public IReadOnlyList<ConfigurationPostCommitIssue> PostCommitIssues { get; init; } = [];
}

/// <summary>
/// Describes the first unapplied request in a best-effort mutation batch.
/// </summary>
public sealed record ConfigurationMutationBatchFailure
{
    /// <summary>
    /// Gets the request identity that failed.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets a concise failure message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets complete exception detail.
    /// </summary>
    public required string Detail { get; init; }
}
