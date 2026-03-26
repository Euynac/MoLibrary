namespace Monica.DevOps.Git.Models;

/// <summary>
/// Read-only repository status snapshot returned to callers.
/// </summary>
public sealed class GitRepositoryStatus
{
    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the provider label.
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Gets the configured remote URL.
    /// </summary>
    public required string RemoteUrl { get; init; }

    /// <summary>
    /// Gets the configured local path.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Gets the resolved absolute local path.
    /// </summary>
    public required string ResolvedLocalPath { get; init; }

    /// <summary>
    /// Gets the configured branch.
    /// </summary>
    public string? ConfiguredBranch { get; init; }

    /// <summary>
    /// Gets the runtime resolved branch.
    /// </summary>
    public string? ResolvedBranch { get; init; }

    /// <summary>
    /// Gets the credential identifier.
    /// </summary>
    public string? CredentialId { get; init; }

    /// <summary>
    /// Gets the current runtime state.
    /// </summary>
    public required GitRepositorySyncState State { get; init; }

    /// <summary>
    /// Gets whether the repository is available on disk.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Gets whether the worktree is currently dirty.
    /// </summary>
    public required bool IsDirty { get; init; }

    /// <summary>
    /// Gets the current commit SHA.
    /// </summary>
    public string? CurrentCommit { get; init; }

    /// <summary>
    /// Gets the current operation progress stage.
    /// </summary>
    public string? ProgressStage { get; init; }

    /// <summary>
    /// Gets the current operation progress percent.
    /// </summary>
    public double? ProgressPercent { get; init; }

    /// <summary>
    /// Gets the current working directory size in bytes.
    /// </summary>
    public required long WorkingDirectorySizeBytes { get; init; }

    /// <summary>
    /// Gets the last synchronization timestamp.
    /// </summary>
    public DateTimeOffset? LastSyncAtUtc { get; init; }

    /// <summary>
    /// Gets the last synchronization message.
    /// </summary>
    public string? LastSyncMessage { get; init; }

    /// <summary>
    /// Gets the last error.
    /// </summary>
    public string? LastError { get; init; }
}
