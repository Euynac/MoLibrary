using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Services;

/// <summary>
/// Lightweight state holder for a single chat session backed by the Microsoft Agent Framework.
/// Replaces the former IChatSession/ChatSession with ChatClientAgent + AgentSession.
/// </summary>
public class AgentSessionState
{
    public AgentSessionState(
        ChatClientAgent agent,
        AgentSession session,
        InMemoryChatHistoryProvider chatHistory,
        string providerId)
    {
        Agent = agent;
        Session = session;
        ChatHistory = chatHistory;
        ProviderId = providerId;
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
    /// Current provider ID
    /// </summary>
    public string ProviderId { get; set; }

    /// <summary>
    /// Current model name
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// System prompt (passed as instructions on each agent run)
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Active knowledge base IDs for this session (for UI display and session recreation).
    /// Null if RAG is not enabled for this session.
    /// </summary>
    public List<string>? ActiveKnowledgeBaseIds { get; set; }

    /// <summary>
    /// The ChatClientAgent instance wrapping the IChatClient
    /// </summary>
    public ChatClientAgent Agent { get; set; }

    /// <summary>
    /// The agent session holding history and context provider references
    /// </summary>
    public AgentSession Session { get; set; }

    /// <summary>
    /// Direct reference to the chat history provider for UI display and manipulation
    /// (implements IList&lt;ChatMessage&gt;)
    /// </summary>
    public InMemoryChatHistoryProvider ChatHistory { get; }

    /// <summary>
    /// Message count in the chat history
    /// </summary>
    public int MessageCount => ChatHistory.Count;

    /// <summary>
    /// Truncate history to keep only the first N messages
    /// </summary>
    public void TruncateHistory(int keepCount)
    {
        if (keepCount < 0) keepCount = 0;
        while (ChatHistory.Count > keepCount)
        {
            ChatHistory.RemoveAt(ChatHistory.Count - 1);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void ClearHistory()
    {
        ChatHistory.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
