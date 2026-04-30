using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Services.Support;

namespace Monica.AI.Models;

/// <summary>
/// Public chat session model shared between infrastructure facades and UI state.
/// Configuration setters automatically mark the internal agent runtime for recreation.
/// </summary>
public class ChatSession
{
    private string _providerId;
    private long _capabilityRevision;

    internal ChatSession(
        AIAgent agent,
        AgentSession session,
        string providerId,
        long capabilityRevision)
    {
        Agent = agent;
        Session = session;
        _providerId = providerId;
        _capabilityRevision = capabilityRevision;
        SessionId = Guid.NewGuid().ToString("N");
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Session title
    /// </summary>
    public string Title { get; set; } = "New Chat";

    /// <summary>
    /// Session creation time
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Last update time
    /// </summary>
    public DateTimeOffset UpdatedAt { get; internal set; }

    /// <summary>
    /// Current provider ID.
    /// Setting this property triggers agent recreation on next message send.
    /// </summary>
    public string ProviderId
    {
        get => _providerId;
        set
        {
            if (_providerId != value)
            {
                _providerId = value;
                NeedsRecreation = true;
            }
        }
    }

    /// <summary>
    /// Current model name.
    /// Setting this property triggers agent recreation on next message send.
    /// </summary>
    public string? ModelName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NeedsRecreation = true;
            }
        }
    }

    /// <summary>
    /// System prompt (passed as instructions on each agent run).
    /// Setting this property triggers agent recreation on next message send.
    /// </summary>
    public string? SystemPrompt
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NeedsRecreation = true;
            }
        }
    }

    /// <summary>
    /// Runtime context visible to skills and tools during the next chat invocation.
    /// </summary>
    public AIChatRuntimeContext RuntimeContext { get; set; } = AIChatRuntimeContext.Empty;

    /// <summary>
    /// Runtime capability-state revision used to create the current agent pipeline.
    /// Setting this property triggers agent recreation on next message send when it changes.
    /// </summary>
    public long CapabilityRevision
    {
        get => _capabilityRevision;
        internal set => _capabilityRevision = value;
    }

    /// <summary>
    /// Marks this session for recreation when the persisted capability-state revision changed.
    /// </summary>
    internal void MarkCapabilityRevision(long capabilityRevision)
    {
        if (_capabilityRevision == capabilityRevision)
        {
            return;
        }

        _capabilityRevision = capabilityRevision;
        NeedsRecreation = true;
    }

    /// <summary>
    /// Whether reasoning/thinking mode is enabled for this session.
    /// This is a per-message option and does NOT require agent recreation.
    /// </summary>
    public bool ReasoningEnabled
    {
        get;
        set => field = value;
    }

    /// <summary>
    /// Indicates whether the agent needs to be recreated due to configuration changes.
    /// This flag is checked before sending messages and reset after recreation.
    /// </summary>
    internal bool NeedsRecreation { get; set; }

    /// <summary>
    /// Message history for UI display.
    /// This list is managed by the UI service layer.
    /// </summary>
    public List<AIChatMessage> Messages { get; } = [];
    
    /// <summary>
    /// The current agent pipeline instance for this session.
    /// </summary>
    internal AIAgent Agent { get; set; }

    /// <summary>
    /// The agent session holding history and context provider references
    /// </summary>
    internal AgentSession Session { get; set; }

    /// <summary>
    /// Gets the internal agent chat history for this session.
    /// </summary>
    internal IList<ChatMessage>? ChatHistory
    {
        get
        {
            var provider = Agent.GetService<InMemoryChatHistoryProvider>();
            return provider?.GetMessages(Session);
        }
    }

    /// <summary>
    /// Gets the internal chat-history message count.
    /// </summary>
    internal int MessageCount => ChatHistory?.Count ?? 0;

    /// <summary>
    /// Reset the recreation flag after agent has been recreated.
    /// This method is called internally by AIChatService after recreation.
    /// </summary>
    internal void ResetRecreationFlag()
    {
        NeedsRecreation = false;
    }

    /// <summary>
    /// Truncate history to keep only the first N messages
    /// </summary>
    internal void TruncateHistory(int keepCount)
    {
        var history = ChatHistory;
        if (history == null) return;

        if (keepCount < 0) keepCount = 0;
        while (history.Count > keepCount)
        {
            history.RemoveAt(history.Count - 1);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    internal void ClearHistory()
    {
        var history = ChatHistory;
        if (history == null) return;

        history.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
