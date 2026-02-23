using Monica.AI.Services;

namespace Monica.AI.UI.Services;

/// <summary>
/// Session storage service for Blazor component state sharing.
/// Manages AgentSessionState instances for the UI layer.
/// </summary>
public class ChatSessionStorage
{
    private readonly List<AgentSessionState> _sessions = [];
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
    public IReadOnlyList<AgentSessionState> Sessions => _sessions.AsReadOnly();

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
    public void AddSession(AgentSessionState session)
    {
        _sessions.Insert(0, session);
        SessionsChanged?.Invoke();
    }

    /// <summary>
    /// Update session
    /// </summary>
    public void UpdateSession(string sessionId, Action<AgentSessionState> updateAction)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session != null)
        {
            updateAction(session);
            SessionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Remove session
    /// </summary>
    public void RemoveSession(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session != null)
        {
            _sessions.Remove(session);
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
    public AgentSessionState? GetSession(string sessionId)
    {
        return _sessions.FirstOrDefault(s => s.SessionId == sessionId);
    }

    /// <summary>
    /// Clear all sessions
    /// </summary>
    public void ClearSessions()
    {
        _sessions.Clear();
        _currentSessionId = null;
        CurrentSessionChanged?.Invoke();
        SessionsChanged?.Invoke();
    }
}
