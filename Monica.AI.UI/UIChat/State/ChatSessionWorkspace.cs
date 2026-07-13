using Monica.AI.Chat.Facades;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Models;
using Monica.Core.Results;

namespace Monica.AI.UI.UIChat.State;

/// <summary>
/// Owns the durable chat catalog, lazily loaded sessions, current selection, and runtime disposal
/// for one Blazor circuit.
/// </summary>
public sealed class ChatSessionWorkspace(ChatHistoryFacade historyFacade) : IAsyncDisposable
{
    private readonly List<ChatSessionSummary> _sessions = [];
    private readonly Dictionary<string, ChatSession> _loadedSessions = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Raised whenever catalog, selection, or loading state changes.</summary>
    public event Action? StateChanged;

    /// <summary>Raised for non-destructive persistence and restoration warnings.</summary>
    public event Action<ChatHistoryWorkspaceWarning>? WarningRaised;

    /// <summary>Whether the initial catalog and selected transcript are being restored.</summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>Whether initial history restoration has completed.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>Current catalog revision used for optimistic writes.</summary>
    public long Revision { get; private set; }

    /// <summary>Persisted session summaries ordered by most recent update.</summary>
    public IReadOnlyList<ChatSessionSummary> Sessions => _sessions;

    /// <summary>Current session identifier.</summary>
    public string? CurrentSessionId { get; private set; }

    /// <summary>Currently loaded transcript, if one is selected.</summary>
    public ChatSession? CurrentSession
        => CurrentSessionId is not null && _loadedSessions.TryGetValue(CurrentSessionId, out var session)
            ? session
            : null;

    /// <summary>Loads the catalog and selected transcript without constructing an agent runtime.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsInitialized)
        {
            return;
        }

        IsLoading = true;
        StateChanged?.Invoke();
        var catalogResult = await historyFacade.GetCatalogAsync(ct);
        if (catalogResult.IsFailed(out var error, out var catalog))
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.LoadFailed, error.Message);
            CompleteInitialization();
            return;
        }

        Revision = catalog.Revision;
        ReplaceSummaries(catalog.Sessions);
        var selectedId = catalog.CurrentSessionId is not null
                         && _sessions.Any(item => item.SessionId == catalog.CurrentSessionId)
            ? catalog.CurrentSessionId
            : _sessions.FirstOrDefault()?.SessionId;
        if (selectedId is not null)
        {
            await LoadSessionAsync(selectedId, ct);
            CurrentSessionId = _loadedSessions.ContainsKey(selectedId) ? selectedId : null;
        }

        CompleteInitialization();
    }

    /// <summary>Adds, persists, and selects a newly created session.</summary>
    public async Task AddSessionAsync(ChatSession session, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);

        _loadedSessions[session.SessionId] = session;
        UpsertSummary(session);
        CurrentSessionId = session.SessionId;
        StateChanged?.Invoke();

        var saved = await SaveSessionAsync(session, ct);
        if (saved)
        {
            await PersistCurrentSelectionAsync(ct);
        }
    }

    /// <summary>Loads and selects one catalog session, then persists the selection.</summary>
    public async Task<ChatSession?> SelectSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (_sessions.All(item => item.SessionId != sessionId))
        {
            return null;
        }

        var session = await LoadSessionAsync(sessionId, ct);
        if (session is null)
        {
            return null;
        }

        CurrentSessionId = sessionId;
        StateChanged?.Invoke();
        await PersistCurrentSelectionAsync(ct);
        return session;
    }

    /// <summary>Persists the complete current state of one loaded session.</summary>
    public async Task<bool> SaveSessionAsync(ChatSession session, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);

        var saveResult = await historyFacade.SaveSessionAsync(session, Revision, ct);
        var accepted = TryApplyWriteResult(saveResult, out var persisted);
        if (persisted is not null)
        {
            await ApplyPrunedSessionsAsync(persisted.PrunedSessionIds);
        }

        if (!accepted)
        {
            return false;
        }

        UpsertSummary(session);
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Deletes one session after the provider accepts the current catalog revision.</summary>
    public async Task<bool> RemoveSessionAsync(string sessionId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var result = await historyFacade.DeleteSessionAsync(sessionId, Revision, ct);
        if (!TryApplyWriteResult(result, out _))
        {
            return false;
        }

        _sessions.RemoveAll(item => item.SessionId == sessionId);
        if (_loadedSessions.Remove(sessionId, out var removed))
        {
            await removed.DisposeAsync();
        }

        if (CurrentSessionId == sessionId)
        {
            CurrentSessionId = _sessions.FirstOrDefault()?.SessionId;
            if (CurrentSessionId is not null)
            {
                await LoadSessionAsync(CurrentSessionId, ct);
            }
        }

        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Clears all sessions after the provider accepts the current catalog revision.</summary>
    public async Task<bool> ClearAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = await historyFacade.ClearAsync(Revision, ct);
        if (!TryApplyWriteResult(result, out _))
        {
            return false;
        }

        var loadedSessions = _loadedSessions.Values.ToArray();
        _loadedSessions.Clear();
        _sessions.Clear();
        CurrentSessionId = null;
        foreach (var session in loadedSessions)
        {
            await session.DisposeAsync();
        }

        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Returns one already loaded session without performing browser I/O.</summary>
    public ChatSession? GetLoadedSession(string? sessionId)
        => sessionId is not null && _loadedSessions.TryGetValue(sessionId, out var session)
            ? session
            : null;

    private async Task<ChatSession?> LoadSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_loadedSessions.TryGetValue(sessionId, out var loaded))
        {
            return loaded;
        }

        var loadResult = await historyFacade.LoadSessionAsync(sessionId, ct);
        if (loadResult.IsFailed(out var error, out var session))
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.LoadFailed, error.Message);
            return null;
        }

        if (session is null)
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.SessionUnavailable, sessionId);
            return null;
        }

        _loadedSessions[sessionId] = session;
        return session;
    }

    private async Task PersistCurrentSelectionAsync(CancellationToken ct)
    {
        var result = await historyFacade.SetCurrentSessionAsync(CurrentSessionId, Revision, ct);
        _ = TryApplyWriteResult(result, out var writeResult);
        if (writeResult is not null)
        {
            await ApplyPrunedSessionsAsync(writeResult.PrunedSessionIds);
        }
    }

    private bool TryApplyWriteResult(
        Res<ChatHistoryWriteResult> result,
        out ChatHistoryWriteResult? writeResult)
    {
        writeResult = null;
        if (result.IsFailed(out var error, out var data))
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.StorageUnavailable, error.Message);
            return false;
        }

        writeResult = data;

        if (writeResult.IsConflict)
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.RevisionConflict);
            return false;
        }

        if (writeResult.Status == ChatHistoryWriteStatus.NotPersisted)
        {
            Revision = Math.Max(Revision, writeResult.Revision);
            if (writeResult.PrunedSessionIds.Count > 0)
            {
                RaiseWarning(ChatHistoryWorkspaceWarningKind.SessionsPruned);
            }

            if (writeResult.FailureReason == ChatHistoryWriteFailureReason.PersistenceDisabled)
            {
                return true;
            }

            RaiseWarning(
                writeResult.FailureReason == ChatHistoryWriteFailureReason.QuotaExceeded
                    ? ChatHistoryWorkspaceWarningKind.QuotaExceeded
                    : ChatHistoryWorkspaceWarningKind.StorageUnavailable,
                writeResult.Warning);
            return false;
        }

        Revision = writeResult.Revision;
        if (writeResult.PrunedSessionIds.Count > 0)
        {
            RaiseWarning(ChatHistoryWorkspaceWarningKind.SessionsPruned);
        }

        return true;
    }

    private async Task ApplyPrunedSessionsAsync(IReadOnlyList<string> prunedSessionIds)
    {
        foreach (var sessionId in prunedSessionIds)
        {
            _sessions.RemoveAll(item => item.SessionId == sessionId);
            if (_loadedSessions.Remove(sessionId, out var session))
            {
                await session.DisposeAsync();
            }
        }
    }

    private void UpsertSummary(ChatSession session)
    {
        _sessions.RemoveAll(item => item.SessionId == session.SessionId);
        _sessions.Add(new ChatSessionSummary
        {
            SessionId = session.SessionId,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Settings = session.Settings,
            Revision = session.PersistenceRevision
        });
        _sessions.Sort(static (left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
    }

    private void ReplaceSummaries(IEnumerable<ChatSessionSummary> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions.OrderByDescending(item => item.UpdatedAt));
    }

    private void CompleteInitialization()
    {
        IsLoading = false;
        IsInitialized = true;
        StateChanged?.Invoke();
    }

    private void RaiseWarning(ChatHistoryWorkspaceWarningKind kind, string? details = null)
        => WarningRaised?.Invoke(new ChatHistoryWorkspaceWarning(kind, details));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var sessions = _loadedSessions.Values.ToArray();
        _loadedSessions.Clear();
        _sessions.Clear();
        foreach (var session in sessions)
        {
            await session.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
