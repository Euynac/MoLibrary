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
    public async Task<AgentSessionState> CreateSessionAsync(
        string? providerId = null,
        string? title = null,
        IEnumerable<string>? knowledgeBaseIds = null,
        CancellationToken ct = default)
    {
        var config = new SessionConfiguration
        {
            ProviderId = providerId,
            Title = title,
            KnowledgeBaseIds = knowledgeBaseIds?.ToList()
        };

        var state = await chatService.CreateSessionAsync(config, ct);

        sessionStorage.AddSession(state);
        sessionStorage.CurrentSessionId = state.SessionId;

        return state;
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
        var state = sessionStorage.GetSession(sessionId);
        if (state != null)
        {
            state.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });

            if (state.Messages.Count == 1)
            {
                state.Title = message.Length > 50 ? message[..50] + "..." : message;
            }

            // Update reasoning mode if changed
            if (state.ReasoningEnabled != reasoningEnabled)
            {
                state.ReasoningEnabled = reasoningEnabled;
            }
        }

        return StreamResponseAsync(sessionId, message, state, ct);
    }

    /// <summary>
    /// Unified stream processing method that accumulates content, reasoning, and tool calls.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> ProcessStreamAsync(
        string sessionId,
        string message,
        AgentSessionState state,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var accumulator = new StreamingContentAccumulator();

        await foreach (var update in chatService.SendMessageStreamingAsync(state, message, ct))
        {
            foreach (var content in update.Contents)
            {
                accumulator.ProcessContent(content);
            }
            yield return update;
        }

        var aiMessage = accumulator.CreateMessage(state.ProviderId ?? string.Empty, state.ModelName ?? string.Empty);
        state.Messages.Add(aiMessage);
        state.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Internal: process streaming response from agent framework.
    /// Captures text, reasoning, and tool call content from the stream.
    /// </summary>
    private IAsyncEnumerable<AgentResponseUpdate> StreamResponseAsync(
        string sessionId,
        string message,
        AgentSessionState? state,
        CancellationToken ct = default)
    {
        if (state == null)
            return AsyncEnumerableEmpty<AgentResponseUpdate>();

        return ProcessStreamAsync(sessionId, message, state, ct);
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
        var state = sessionStorage.GetSession(sessionId);
        if (state == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var index = state.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Remove all messages from this index onwards
        state.Messages.RemoveRange(index, state.Messages.Count - index);

        // Sync backend: truncate agent history to match UI state
        state.TruncateHistory(index);

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
        var state = sessionStorage.GetSession(sessionId);
        if (state == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var index = state.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var userMessage = state.Messages.Take(index).LastOrDefault(m => m.Role == AIChatRole.User);
        if (userMessage == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Remove the AI message and any after it
        state.Messages.RemoveRange(index, state.Messages.Count - index);

        // Sync backend: truncate agent history to match UI state
        state.TruncateHistory(index);

        // Stream new response without adding user message (it already exists in UI)
        return StreamResponseOnlyAsync(sessionId, userMessage.Content, state, ct);
    }

    /// <summary>
    /// Internal: Stream AI response without adding user message to local storage
    /// (used by retry where user message already exists).
    /// Also captures tool call content from the stream.
    /// </summary>
    private IAsyncEnumerable<AgentResponseUpdate> StreamResponseOnlyAsync(
        string sessionId,
        string message,
        AgentSessionState state,
        CancellationToken ct = default)
    {
        return ProcessStreamAsync(sessionId, message, state, ct);
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
        sessionStorage.RemoveSession(sessionId);
        return true;
    }

    /// <summary>
    /// Switch active session
    /// </summary>
    public void SwitchSession(string sessionId)
    {
        sessionStorage.CurrentSessionId = sessionId;
    }

    /// <summary>
    /// Update session system prompt.
    /// Sets the NeedsRecreation flag; agent will be recreated on next message send.
    /// </summary>
    public Task<bool> UpdateSessionSystemPromptAsync(
        string sessionId,
        string? systemPrompt,
        CancellationToken ct = default)
    {
        var state = sessionStorage.GetSession(sessionId);
        if (state == null)
        {
            return Task.FromResult(false);
        }

        state.SystemPrompt = systemPrompt;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        return Task.FromResult(true);
    }

    /// <summary>
    /// Update session configuration.
    /// Changes to ProviderId, ModelName, SystemPrompt, KnowledgeBaseIds, or ReasoningEnabled
    /// will trigger agent recreation on next message send.
    /// </summary>
    public void UpdateSessionConfiguration(string sessionId, SessionConfiguration config)
    {
        var state = sessionStorage.GetSession(sessionId);
        if (state == null) return;

        if (!string.IsNullOrEmpty(config.ProviderId))
            state.ProviderId = config.ProviderId;

        if (config.ModelName != null)
            state.ModelName = config.ModelName;

        if (config.SystemPrompt != null)
            state.SystemPrompt = config.SystemPrompt;

        if (config.KnowledgeBaseIds != null)
            state.ActiveKnowledgeBaseIds = config.KnowledgeBaseIds;

        state.ReasoningEnabled = config.ReasoningEnabled;

        if (!string.IsNullOrEmpty(config.Title))
            state.Title = config.Title;

        state.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
