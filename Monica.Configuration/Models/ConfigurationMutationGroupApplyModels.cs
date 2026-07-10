namespace Monica.Configuration.Models;

/// <summary>
/// Applies a reviewed set of configuration mutations as one operator-visible group.
/// </summary>
public sealed record ConfigurationMutationGroupApplyRequest
{
    /// <summary>
    /// Gets the operator-facing group label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the optional reason for the group.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the audit context shared by every command.
    /// </summary>
    public ConfigurationMutationContext Context { get; init; } = new();

    /// <summary>
    /// Gets the commands in deterministic application order.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationCommand> Commands { get; init; } = [];
}

/// <summary>
/// Describes one command in a configuration mutation group.
/// </summary>
public sealed record ConfigurationMutationCommand
{
    /// <summary>
    /// Gets the caller-owned identity used to correlate outcomes with submitted commands.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the target definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the requested mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the JSON value used by set mutations.
    /// </summary>
    public required ConfigurationStoredValue Value { get; init; }

    /// <summary>
    /// Gets the schema version reviewed by the caller.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the persistence target and its optimistic concurrency token.
    /// </summary>
    public required ConfigurationMutationTarget Target { get; init; }
}

/// <summary>
/// Identifies a persistence target for a configuration mutation command.
/// </summary>
public abstract record ConfigurationMutationTarget;

/// <summary>
/// Targets Monica's effective-value store.
/// </summary>
public sealed record ConfigurationEffectiveStoreMutationTarget : ConfigurationMutationTarget
{
    /// <summary>
    /// Gets the document version observed when the command was staged.
    /// </summary>
    public long? ExpectedVersion { get; init; }
}

/// <summary>
/// Targets a writable external Microsoft configuration source.
/// </summary>
public sealed record ConfigurationExternalSourceMutationTarget : ConfigurationMutationTarget
{
    /// <summary>
    /// Gets the external source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the source content revision observed when the command was staged.
    /// </summary>
    public string? ExpectedRevision { get; init; }
}

/// <summary>
/// Describes the outcome of one submitted mutation command.
/// </summary>
public sealed record ConfigurationMutationOutcome
{
    /// <summary>
    /// Gets the submitted request identity.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the outcome state.
    /// </summary>
    public ConfigurationMutationOutcomeStatus Status { get; init; }

    /// <summary>
    /// Gets the applied mutation result when the command succeeded.
    /// </summary>
    public ConfigurationMutationResult? Result { get; init; }

    /// <summary>
    /// Gets the failure or skip diagnostic when the command was not applied.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Describes the result of applying a configuration mutation group.
/// </summary>
public sealed record ConfigurationMutationGroupApplyResult
{
    /// <summary>
    /// Gets the final group state.
    /// </summary>
    public ConfigurationMutationGroupApplyStatus Status { get; init; }

    /// <summary>
    /// Gets the persisted mutation group.
    /// </summary>
    public required ConfigurationMutationGroup MutationGroup { get; init; }

    /// <summary>
    /// Gets outcomes in submitted command order.
    /// </summary>
    public IReadOnlyList<ConfigurationMutationOutcome> Outcomes { get; init; } = [];

    /// <summary>
    /// Gets failures that occurred after one or more values were durably committed.
    /// </summary>
    public IReadOnlyList<ConfigurationPostCommitIssue> PostCommitIssues { get; init; } = [];
}
