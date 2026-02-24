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
    ChatSessionStateManager stateManager)
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
    /// Create a new session with the specified configuration.
    /// </summary>
    public async Task<AgentSessionState> CreateSessionAsync(
        string? providerId = null,
        string? modelName = null,
        string? systemPrompt = null,
        List<string>? knowledgeBaseIds = null,
        bool reasoningEnabled = false,
        string? title = null,
        CancellationToken ct = default)
    {
        var state = await chatService.CreateSessionAsync(
            providerId,
            modelName,
            systemPrompt,
            knowledgeBaseIds,
            reasoningEnabled,
            title,
            ct);

        sessionStorage.AddSession(state);
        sessionStorage.CurrentSessionId = state.SessionId;

        return state;
    }

    /// <summary>
    /// Send a message and get a streaming response.
    /// Note: UI layer should call StateManager.AddUserMessage() and StateManager.UpdateTitle() before calling this.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        string sessionId,
        string message,
        CancellationToken ct = default)
    {
        var state = sessionStorage.GetSession(sessionId);
        if (state == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
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
        stateManager.AddAssistantMessage(sessionId, aiMessage);
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

        return SendMessageStreamingAsync(sessionId, newContent, ct);
    }

    /// <summary>
    /// Retry an AI message (regenerate response for the previous user message).
    /// Message removal is performed synchronously before returning the async enumerable.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> RetryMessageAsync(
        string sessionId,
        string messageId,
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

        // Find the user message index
        var userMessageIndex = state.Messages.FindIndex(m => m.Id == userMessage.Id);

        // Remove the user message and everything after it (including the AI message to retry)
        state.Messages.RemoveRange(userMessageIndex, state.Messages.Count - userMessageIndex);

        // Sync backend: truncate to remove both user and assistant messages
        state.TruncateHistory(userMessageIndex);

        // Now send the message normally - it will add the user message and new response
        return SendMessageStreamingAsync(sessionId, userMessage.Content, ct);
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
}
