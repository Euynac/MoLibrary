using Monica.AI.Models;

namespace Monica.AI.UI.UIChat.State;

/// <summary>
/// Session storage service for Blazor component state sharing.
/// Manages ChatSession instances for the UI layer.
/// </summary>
public sealed class ChatSessionStore : IAsyncDisposable
{
    private readonly List<ChatSession> _sessions = [];
    private string? _currentSessionId;

    /// <summary>
    /// Current session ID
    /// </summary>
    public string? CurrentSessionId
    {
        get => _currentSessionId;
        set
        {
            if (_currentSessionId != value)
            {
                _currentSessionId = value;
                CurrentSessionChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// All sessions list
    /// </summary>
    public IReadOnlyList<ChatSession> Sessions => _sessions.AsReadOnly();

    /// <summary>
    /// Current session instance resolved from the current session identifier.
    /// </summary>
    public ChatSession? CurrentSession => GetSession(_currentSessionId);

    /// <summary>
    /// Current session changed event
    /// </summary>
    public event Action? CurrentSessionChanged;

    /// <summary>
    /// Sessions list changed event
    /// </summary>
    public event Action? SessionsChanged;

    /// <summary>
    /// Add session
    /// </summary>
    public void AddSession(ChatSession session)
    {
        _sessions.Insert(0, session);
        SessionsChanged?.Invoke();
    }

    /// <summary>
    /// Update session
    /// </summary>
    public bool UpdateSession(
        string sessionId,
        Action<ChatSession> updateAction,
        bool notifySessionsChanged = false,
        bool notifyCurrentSessionChanged = false)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session == null)
        {
            return false;
        }

        updateAction(session);

        if (notifyCurrentSessionChanged
            && string.Equals(_currentSessionId, sessionId, StringComparison.Ordinal))
        {
            CurrentSessionChanged?.Invoke();
        }

        if (notifySessionsChanged)
        {
            SessionsChanged?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Remove session
    /// </summary>
    public async Task RemoveSessionAsync(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session != null)
        {
            _sessions.Remove(session);
            await session.DisposeAsync();
            if (_currentSessionId == sessionId)
            {
                _currentSessionId = _sessions.FirstOrDefault()?.SessionId;
                CurrentSessionChanged?.Invoke();
            }
            SessionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Get session
    /// </summary>
    public ChatSession? GetSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        return _sessions.FirstOrDefault(s => s.SessionId == sessionId);
    }

    /// <summary>
    /// Clear all sessions
    /// </summary>
    public async Task ClearSessionsAsync()
    {
        var sessions = _sessions.ToList();
        _sessions.Clear();
        _currentSessionId = null;
        foreach (var session in sessions)
        {
            await session.DisposeAsync();
        }

        CurrentSessionChanged?.Invoke();
        SessionsChanged?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ClearSessionsAsync();
        GC.SuppressFinalize(this);
    }
}
