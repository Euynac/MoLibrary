using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.Extensions;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.UI.Modules;

namespace Monica.AI.UI.Services;

/// <summary>
/// AI chat UI service - bridges the agent framework with Blazor UI components.
/// </summary>
public class AIChatUIService(
    AIChatService chatService,
    IAIProviderFactory providerFactory,
    ChatSessionStorage sessionStorage,
    IOptions<ModuleAIUIOption> options)
{
    /// <summary>
    /// Get session storage
    /// </summary>
    public ChatSessionStorage SessionStorage => sessionStorage;

    /// <summary>
    /// Get all provider info
    /// </summary>
    public IReadOnlyList<AIProviderInfo> GetProviders()
    {
        return providerFactory.GetAllProviderInfos();
    }

    /// <summary>
    /// Get default provider
    /// </summary>
    public AIProviderInfo? GetDefaultProvider()
    {
        return providerFactory.GetDefaultProvider()?.Info;
    }

    /// <summary>
    /// Create a new session with optional RAG knowledge bases.
    /// </summary>
    public async Task<ChatSessionInfo> CreateSessionAsync(
        string? providerId = null,
        string? title = null,
        IEnumerable<string>? knowledgeBaseIds = null,
        CancellationToken ct = default)
    {
        var state = await chatService.CreateSessionAsync(providerId, title, null, knowledgeBaseIds, ct);

        var sessionInfo = new ChatSessionInfo
        {
            SessionId = state.SessionId,
            Title = state.Title,
            ProviderId = state.ProviderId,
            ModelName = state.ModelName,
            SystemPrompt = state.SystemPrompt,
            ActiveKnowledgeBaseIds = state.ActiveKnowledgeBaseIds
        };

        sessionStorage.AddSession(sessionInfo);
        sessionStorage.CurrentSessionId = state.SessionId;

        return sessionInfo;
    }

    /// <summary>
    /// Send a message and get a streaming response.
    /// User message is added synchronously before returning the async enumerable.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        string sessionId,
        string message,
        bool reasoningEnabled = false,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo != null)
        {
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });

            if (sessionInfo.Messages.Count == 1)
            {
                sessionInfo.Title = message.Length > 50 ? message[..50] + "..." : message;
            }
        }

        return StreamResponseAsync(sessionId, message, sessionInfo, reasoningEnabled, ct);
    }

    /// <summary>
    /// Unified stream processing method that accumulates content, reasoning, and tool calls.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> ProcessStreamAsync(
        string sessionId,
        string message,
        ChatSessionInfo sessionInfo,
        bool reasoningEnabled,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = true,
            ProviderId = sessionInfo.ProviderId,
            ModelName = sessionInfo.ModelName,
            ReasoningEnabled = reasoningEnabled
        };

        var accumulator = new StreamingContentAccumulator();

        await foreach (var update in chatService.SendMessageStreamingAsync(request, ct))
        {
            foreach (var content in update.Contents)
            {
                accumulator.ProcessContent(content);
            }
            yield return update;
        }

        var aiMessage = accumulator.CreateMessage(sessionInfo.ProviderId ?? string.Empty, sessionInfo.ModelName ?? string.Empty);
        sessionInfo.Messages.Add(aiMessage);
        sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Internal: process streaming response from agent framework.
    /// Captures text, reasoning, and tool call content from the stream.
    /// </summary>
    private IAsyncEnumerable<AgentResponseUpdate> StreamResponseAsync(
        string sessionId,
        string message,
        ChatSessionInfo? sessionInfo,
        bool reasoningEnabled = false,
        CancellationToken ct = default)
    {
        if (sessionInfo == null)
            return AsyncEnumerableEmpty<AgentResponseUpdate>();

        return ProcessStreamAsync(sessionId, message, sessionInfo, reasoningEnabled, ct);
    }

    /// <summary>
    /// Edit a user message and resend (discards all messages after it).
    /// Message removal is performed synchronously before returning the async enumerable.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> EditMessageAsync(
        string sessionId,
        string messageId,
        string newContent,
        bool reasoningEnabled = false,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var index = sessionInfo.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Remove all messages from this index onwards
        sessionInfo.Messages.RemoveRange(index, sessionInfo.Messages.Count - index);

        // Sync backend: truncate agent history to match UI state
        chatService.TruncateSessionHistory(sessionId, index);

        return SendMessageStreamingAsync(sessionId, newContent, reasoningEnabled, ct);
    }

    /// <summary>
    /// Retry an AI message (regenerate response for the previous user message).
    /// Message removal is performed synchronously before returning the async enumerable.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> RetryMessageAsync(
        string sessionId,
        string messageId,
        bool reasoningEnabled = false,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var index = sessionInfo.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var userMessage = sessionInfo.Messages.Take(index).LastOrDefault(m => m.Role == AIChatRole.User);
        if (userMessage == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Remove the AI message and any after it
        sessionInfo.Messages.RemoveRange(index, sessionInfo.Messages.Count - index);

        // Sync backend: truncate agent history to match UI state
        chatService.TruncateSessionHistory(sessionId, index);

        // Stream new response without adding user message (it already exists in UI)
        return StreamResponseOnlyAsync(sessionId, userMessage.Content, sessionInfo, reasoningEnabled, ct);
    }

    /// <summary>
    /// Internal: Stream AI response without adding user message to local storage
    /// (used by retry where user message already exists).
    /// Also captures tool call content from the stream.
    /// </summary>
    private IAsyncEnumerable<AgentResponseUpdate> StreamResponseOnlyAsync(
        string sessionId,
        string message,
        ChatSessionInfo sessionInfo,
        bool reasoningEnabled = false,
        CancellationToken ct = default)
    {
        return ProcessStreamAsync(sessionId, message, sessionInfo, reasoningEnabled, ct);
    }

    /// <summary>
    /// Helper to return an empty async enumerable
    /// </summary>
    private static async IAsyncEnumerable<T> AsyncEnumerableEmpty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        var deleted = chatService.DeleteSession(sessionId);
        if (deleted)
        {
            sessionStorage.RemoveSession(sessionId);
        }
        return deleted;
    }

    /// <summary>
    /// Switch active session
    /// </summary>
    public void SwitchSession(string sessionId)
    {
        sessionStorage.CurrentSessionId = sessionId;
    }

    /// <summary>
    /// Update session system prompt
    /// </summary>
    public async Task<bool> UpdateSessionSystemPromptAsync(
        string sessionId,
        string? systemPrompt,
        CancellationToken ct = default)
    {
        if (!await chatService.UpdateSessionSystemPromptAsync(sessionId, systemPrompt, ct))
        {
            return false;
        }

        var session = sessionStorage.GetSession(sessionId);
        if (session != null)
        {
            session.SystemPrompt = systemPrompt;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return true;
    }

    /// <summary>
    /// Load sessions from backend
    /// </summary>
    public void LoadSessions()
    {
        var sessions = chatService.GetAllSessions();
        var sessionInfos = sessions.Select(s => new ChatSessionInfo
        {
            SessionId = s.SessionId,
            Title = s.Title,
            ProviderId = s.ProviderId,
            ModelName = s.ModelName,
            SystemPrompt = s.SystemPrompt,
            ActiveKnowledgeBaseIds = s.ActiveKnowledgeBaseIds,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        sessionStorage.LoadSessions(sessionInfos);
    }
}
