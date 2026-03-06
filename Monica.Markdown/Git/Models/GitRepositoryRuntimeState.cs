namespace Monica.Markdown.Git.Models;

/// <summary>
/// Mutable runtime state for a configured repository.
/// </summary>
internal sealed class GitRepositoryRuntimeState
{
    public GitRepositorySyncState State { get; set; } = GitRepositorySyncState.NotReady;

    public string? ResolvedBranch { get; set; }

    public string? CurrentCommit { get; set; }

    public DateTimeOffset? LastSyncAtUtc { get; set; }

    public string? LastError { get; set; }

    public string? LastSyncMessage { get; set; }

    public bool IsDirty { get; set; }

    public bool IsAvailable { get; set; }
}
