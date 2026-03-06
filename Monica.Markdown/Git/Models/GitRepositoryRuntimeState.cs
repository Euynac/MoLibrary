using LibGit2Sharp;

namespace Monica.Markdown.Git.Models;

/// <summary>
/// Mutable runtime state for a configured repository.
/// </summary>
internal sealed class GitRepositoryRuntimeState
{
    private readonly object _syncRoot = new();

    private GitRepositorySyncState State { get; set; } = GitRepositorySyncState.NotReady;

    private string? ResolvedBranch { get; set; }

    private string? CurrentCommit { get; set; }

    private DateTimeOffset? LastSyncAtUtc { get; set; }

    private string? LastError { get; set; }

    private string? LastSyncMessage { get; set; }

    private string? ProgressStage { get; set; }

    private double? ProgressPercent { get; set; }

    private long WorkingDirectorySizeBytes { get; set; }

    private bool IsDirty { get; set; }

    private bool IsAvailable { get; set; }

    public GitRepositoryRuntimeSnapshot Capture()
    {
        lock (_syncRoot)
        {
            return new GitRepositoryRuntimeSnapshot(
                State,
                ResolvedBranch,
                CurrentCommit,
                ProgressStage,
                ProgressPercent,
                LastSyncAtUtc,
                LastSyncMessage,
                LastError,
                IsDirty,
                IsAvailable,
                WorkingDirectorySizeBytes);
        }
    }

    public void BeginSync(string resolvedLocalPath)
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.Syncing;
            LastError = null;
            LastSyncMessage = null;
            ProgressStage = GitRepositoryProgressStages.Preparing;
            ProgressPercent = null;
            UpdateDirectoryMetrics(resolvedLocalPath);
        }
    }

    public void BeginDeletion()
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.Syncing;
            ProgressStage = GitRepositoryProgressStages.Deleting;
            ProgressPercent = null;
            LastError = null;
            LastSyncMessage = null;
        }
    }

    public void CompleteDeletion(string deleteMessage)
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.NotReady;
            IsAvailable = false;
            IsDirty = false;
            CurrentCommit = null;
            ResolvedBranch = null;
            LastError = null;
            LastSyncMessage = deleteMessage;
            ProgressStage = null;
            ProgressPercent = null;
            WorkingDirectorySizeBytes = 0;
        }
    }

    public void CompleteSync(
        Repository repository,
        string resolvedLocalPath,
        string targetBranchName,
        string syncMessage,
        DateTimeOffset syncedAtUtc)
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.Ready;
            IsAvailable = true;
            IsDirty = repository.RetrieveStatus().IsDirty;
            CurrentCommit = repository.Head.Tip?.Sha;
            LastSyncAtUtc = syncedAtUtc;
            ResolvedBranch = targetBranchName;
            LastSyncMessage = syncMessage;
            LastError = null;
            ProgressStage = null;
            ProgressPercent = null;
            UpdateWorkingDirectorySize(resolvedLocalPath);
        }
    }

    public void MarkFailed(string errorMessage)
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.Failed;
            LastError = errorMessage;
            LastSyncMessage = errorMessage;
            ProgressStage = null;
            ProgressPercent = null;
        }
    }

    public void UpdateOperationProgress(string progressStage, double? progressPercent, string resolvedLocalPath)
    {
        lock (_syncRoot)
        {
            State = GitRepositorySyncState.Syncing;
            ProgressStage = progressStage;
            ProgressPercent = progressPercent;
            UpdateDirectoryMetrics(resolvedLocalPath);
        }
    }

    public bool TryRefreshMetricsWhileSyncing(string resolvedLocalPath)
    {
        lock (_syncRoot)
        {
            if (State != GitRepositorySyncState.Syncing)
            {
                return false;
            }

            UpdateDirectoryMetrics(resolvedLocalPath);
            return true;
        }
    }

    public void MarkNotReadyFromDisk()
    {
        lock (_syncRoot)
        {
            if (State == GitRepositorySyncState.Syncing)
            {
                return;
            }

            State = GitRepositorySyncState.NotReady;
            IsAvailable = false;
            IsDirty = false;
            CurrentCommit = null;
            ResolvedBranch = null;
            ProgressStage = null;
            ProgressPercent = null;
            WorkingDirectorySizeBytes = 0;
        }
    }

    public void RefreshFromRepository(string? resolvedBranch, string? currentCommit, bool isDirty, string resolvedLocalPath)
    {
        lock (_syncRoot)
        {
            if (State == GitRepositorySyncState.Syncing)
            {
                return;
            }

            if (State == GitRepositorySyncState.NotReady)
            {
                State = GitRepositorySyncState.Ready;
            }

            IsAvailable = true;
            IsDirty = isDirty;
            CurrentCommit = currentCommit;
            ResolvedBranch = resolvedBranch;
            ProgressStage = null;
            ProgressPercent = null;
            UpdateWorkingDirectorySize(resolvedLocalPath, 0);
        }
    }

    private void UpdateDirectoryMetrics(string resolvedLocalPath)
    {
        IsAvailable = Directory.Exists(resolvedLocalPath);
        UpdateWorkingDirectorySize(resolvedLocalPath);
    }

    private void UpdateWorkingDirectorySize(string resolvedLocalPath, long? fallbackValue = null)
    {
        WorkingDirectorySizeBytes = TryGetWorkingDirectorySizeBytes(
            resolvedLocalPath,
            fallbackValue ?? WorkingDirectorySizeBytes);
    }

    private static long TryGetWorkingDirectorySizeBytes(string resolvedLocalPath, long fallbackValue)
    {
        if (!Directory.Exists(resolvedLocalPath))
        {
            return 0;
        }

        try
        {
            long total = 0;
            foreach (var filePath in Directory.EnumerateFiles(resolvedLocalPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(filePath).Length;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return total;
        }
        catch (IOException)
        {
            return fallbackValue;
        }
        catch (UnauthorizedAccessException)
        {
            return fallbackValue;
        }
    }
}

internal readonly record struct GitRepositoryRuntimeSnapshot(
    GitRepositorySyncState State,
    string? ResolvedBranch,
    string? CurrentCommit,
    string? ProgressStage,
    double? ProgressPercent,
    DateTimeOffset? LastSyncAtUtc,
    string? LastSyncMessage,
    string? LastError,
    bool IsDirty,
    bool IsAvailable,
    long WorkingDirectorySizeBytes);
