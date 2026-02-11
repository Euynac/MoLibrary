using Monica.AI.Models;

namespace Monica.AI.UI.Services;

/// <summary>
/// 会话本地存储服务（用于 Blazor 组件间状态共享）
/// </summary>
public class ChatSessionStorage
{
    private readonly List<ChatSessionInfo> _sessions = [];
    private string? _currentSessionId;

    /// <summary>
    /// 当前会话 ID
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
    /// 所有会话列表
    /// </summary>
    public IReadOnlyList<ChatSessionInfo> Sessions => _sessions.AsReadOnly();

    /// <summary>
    /// 当前会话变更事件
    /// </summary>
    public event Action? CurrentSessionChanged;

    /// <summary>
    /// 会话列表变更事件
    /// </summary>
    public event Action? SessionsChanged;

    /// <summary>
    /// 添加会话
    /// </summary>
    public void AddSession(ChatSessionInfo session)
    {
        _sessions.Insert(0, session);
        SessionsChanged?.Invoke();
    }

    /// <summary>
    /// 更新会话
    /// </summary>
    public void UpdateSession(string sessionId, Action<ChatSessionInfo> updateAction)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session != null)
        {
            updateAction(session);
            SessionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 删除会话
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
    /// 获取会话
    /// </summary>
    public ChatSessionInfo? GetSession(string sessionId)
    {
        return _sessions.FirstOrDefault(s => s.SessionId == sessionId);
    }

    /// <summary>
    /// 清空所有会话
    /// </summary>
    public void ClearSessions()
    {
        _sessions.Clear();
        _currentSessionId = null;
        CurrentSessionChanged?.Invoke();
        SessionsChanged?.Invoke();
    }

    /// <summary>
    /// 加载会话列表（从后端同步）
    /// </summary>
    public void LoadSessions(IEnumerable<ChatSessionInfo> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions);
        SessionsChanged?.Invoke();
    }
}

/// <summary>
/// 会话信息模型（UI 使用）
/// </summary>
public class ChatSessionInfo
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title { get; set; } = "新对话";

    /// <summary>
    /// Provider ID
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// System prompt
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Active knowledge base IDs for this session.
    /// Null if RAG is not enabled.
    /// </summary>
    public List<string>? ActiveKnowledgeBaseIds { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 消息列表
    /// </summary>
    public List<AIChatMessage> Messages { get; } = [];
}
