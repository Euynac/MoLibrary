namespace Monica.Configuration.Models;

/// <summary>
/// Carries distributed configuration invalidation metadata without configuration values.
/// </summary>
public sealed record ConfigurationReloadSignal
{
    /// <summary>
    /// Gets the unique signal identity.
    /// </summary>
    public required string SignalId { get; init; }

    /// <summary>
    /// Gets the service instance that created the signal.
    /// </summary>
    public required string OriginInstanceId { get; init; }

    /// <summary>
    /// Gets the authoritative store that produced the signal.
    /// </summary>
    public required string StoreKey { get; init; }

    /// <summary>
    /// Gets the invalidation kind.
    /// </summary>
    public ConfigurationReloadSignalKind Kind { get; init; }

    /// <summary>
    /// Gets changed definition versions for a targeted invalidation.
    /// </summary>
    public IReadOnlyList<ConfigurationReloadDefinitionVersion> Definitions { get; init; } = [];

    /// <summary>
    /// Gets when the signal was created.
    /// </summary>
    public DateTimeOffset ChangedTime { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Identifies the minimum authoritative version to load for one definition.
/// </summary>
public sealed record ConfigurationReloadDefinitionVersion
{
    /// <summary>
    /// Gets the definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the minimum document version to load, when known.
    /// </summary>
    public long? Version { get; init; }
}

/// <summary>
/// Describes the local and distributed outcome of a reload-all broadcast.
/// </summary>
public sealed record ConfigurationReloadBroadcastResult
{
    /// <summary>
    /// Gets whether the initiating process reloaded its Monica projection successfully.
    /// </summary>
    public bool LocalReloadSucceeded { get; init; }

    /// <summary>
    /// Gets local reload or distributed publication issues.
    /// </summary>
    public IReadOnlyList<ConfigurationPostCommitIssue> PostCommitIssues { get; init; } = [];

    /// <summary>
    /// Gets whether both local reload and distributed publication completed without a reported issue.
    /// </summary>
    public bool IsSuccessful => LocalReloadSucceeded && PostCommitIssues.Count == 0;
}
