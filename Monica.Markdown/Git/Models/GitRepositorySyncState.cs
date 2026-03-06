namespace Monica.Markdown.Git.Models;

/// <summary>
/// Represents the current runtime synchronization state of a repository.
/// </summary>
public enum GitRepositorySyncState
{
    NotReady = 1,
    Syncing = 2,
    Ready = 3,
    Failed = 4
}
