using Monica.AI.Models;

namespace Monica.AI.UI.Services;

/// <summary>
/// Centralized manager for AgentSessionState mutations.
/// Ensures single source of truth for state changes.
/// </summary>
public class ChatSessionStateManager(ChatSessionStorage storage)
{
    /// <summary>
    /// Update session reasoning mode without triggering agent recreation.
    /// </summary>
    public void UpdateReasoning(string sessionId, bool enabled)
    {
        var session = storage.GetSession(sessionId);
        if (session != null)
        {
            session.ReasoningEnabled = enabled;
        }
    }

    /// <summary>
    /// Add user message to session.
    /// </summary>
    public void AddUserMessage(string sessionId, string content)
    {
        var session = storage.GetSession(sessionId);
        if (session == null) return;

        session.Messages.Add(new AIChatMessage
        {
            Role = AIChatRole.User,
            Content = content
        });
    }

    /// <summary>
    /// Add assistant message to session.
    /// </summary>
    public void AddAssistantMessage(string sessionId, AIChatMessage message)
    {
        var session = storage.GetSession(sessionId);
        if (session == null) return;

        session.Messages.Add(message);
    }

    /// <summary>
    /// Update session title.
    /// </summary>
    public void UpdateTitle(string sessionId, string title)
    {
        var session = storage.GetSession(sessionId);
        if (session != null)
        {
            session.Title = title;
        }
    }

    /// <summary>
    /// Clear session messages.
    /// </summary>
    public void ClearMessages(string sessionId)
    {
        var session = storage.GetSession(sessionId);
        if (session != null)
        {
            session.Messages.Clear();
        }
    }
}
