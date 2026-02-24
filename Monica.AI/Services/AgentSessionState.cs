using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Models;

namespace Monica.AI.Services;

/// <summary>
/// Unified state holder for a single chat session backed by the Microsoft Agent Framework.
/// Combines backend agent state with UI message history.
/// Smart property setters automatically trigger agent recreation when configuration changes.
/// </summary>
public class AgentSessionState
{
    private string _providerId;

    public AgentSessionState(
        ChatClientAgent agent,
        AgentSession session,
        string providerId)
    {
        Agent = agent;
        Session = session;
        _providerId = providerId;
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
    public DateTimeOffset UpdatedAt { get; set; }

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
    /// Active knowledge base IDs for this session (for RAG integration).
    /// Null if RAG is not enabled for this session.
    /// Setting this property triggers agent recreation on next message send.
    /// </summary>
    public List<string>? ActiveKnowledgeBaseIds
    {
        get;
        set
        {
            // Compare list contents, not reference
            var needsUpdate = false;
            if (field == null && value != null)
                needsUpdate = true;
            else if (field != null && value == null)
                needsUpdate = true;
            else if (field != null && value != null)
            {
                if (field.Count != value.Count ||
                    !field.SequenceEqual(value))
                    needsUpdate = true;
            }

            if (needsUpdate)
            {
                field = value;
                NeedsRecreation = true;
            }
        }
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
    public bool NeedsRecreation { get; internal set; }

    /// <summary>
    /// Message history for UI display.
    /// This list is managed by the UI service layer.
    /// </summary>
    public List<AIChatMessage> Messages { get; } = [];
    
    /// <summary>
    /// The ChatClientAgent instance wrapping the IChatClient
    /// </summary>
    public ChatClientAgent Agent { get; internal set; }

    /// <summary>
    /// The agent session holding history and context provider references
    /// </summary>
    public AgentSession Session { get; internal set; }

    /// <summary>
    /// Get the chat history from the session
    /// </summary>
    public IList<ChatMessage>? ChatHistory => Session.GetService<IList<ChatMessage>>();

    /// <summary>
    /// Message count in the chat history
    /// </summary>
    public int MessageCount => ChatHistory?.Count ?? 0;

    /// <summary>
    /// Reset the recreation flag after agent has been recreated.
    /// This method is called internally by AIChatService after recreation.
    /// </summary>
    public void ResetRecreationFlag()
    {
        NeedsRecreation = false;
    }

    /// <summary>
    /// Truncate history to keep only the first N messages
    /// </summary>
    public void TruncateHistory(int keepCount)
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
    public void ClearHistory()
    {
        var history = ChatHistory;
        if (history == null) return;

        history.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
