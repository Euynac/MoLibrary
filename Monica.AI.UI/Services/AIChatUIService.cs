using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
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
    private readonly ModuleAIUIOption _options = options.Value;

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
    /// Get or create a session
    /// </summary>
    public async Task<ChatSessionInfo> GetOrCreateSessionAsync(
        string? sessionId = null,
        string? providerId = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            var existing = sessionStorage.GetSession(sessionId);
            if (existing != null)
            {
                return existing;
            }
        }

        return await CreateSessionAsync(providerId, ct: ct);
    }

    /// <summary>
    /// Send a message and get a non-streaming response
    /// </summary>
    public async Task<AIChatMessage> SendMessageAsync(
        string sessionId,
        string message,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = false,
            ProviderId = sessionInfo?.ProviderId
        };

        var response = await chatService.SendMessageAsync(request, ct);

        if (sessionInfo != null)
        {
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });
            sessionInfo.Messages.Add(response.Message);
            sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;

            if (sessionInfo.Messages.Count == 2)
            {
                sessionInfo.Title = message.Length > 50 ? message[..50] + "..." : message;
            }
        }

        return response.Message;
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
    /// Internal: process streaming response from agent framework.
    /// Captures text, reasoning, and tool call content from the stream.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> StreamResponseAsync(
        string sessionId,
        string message,
        ChatSessionInfo? sessionInfo,
        bool reasoningEnabled = false,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = true,
            ProviderId = sessionInfo?.ProviderId,
            ReasoningEnabled = reasoningEnabled
        };

        var fullContent = string.Empty;
        var fullReasoning = string.Empty;
        var reasoningStopwatch = new Stopwatch();
        var toolCalls = new List<ToolCallInfo>();

        await foreach (var update in chatService.SendMessageStreamingAsync(request, ct))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextReasoningContent reasoning && !string.IsNullOrEmpty(reasoning.Text))
                {
                    if (!reasoningStopwatch.IsRunning)
                        reasoningStopwatch.Start();
                    fullReasoning += reasoning.Text;
                }
                else if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                {
                    if (reasoningStopwatch.IsRunning)
                        reasoningStopwatch.Stop();
                    fullContent += text.Text;
                }
                else if (content is FunctionCallContent functionCall)
                {
                    toolCalls.Add(new ToolCallInfo(
                        functionCall.Name,
                        functionCall.CallId ?? string.Empty,
                        functionCall.Arguments,
                        null,
                        DateTimeOffset.UtcNow));
                }
                else if (content is FunctionResultContent functionResult)
                {
                    var matching = toolCalls.FindIndex(t => t.CallId == functionResult.CallId);
                    if (matching >= 0)
                    {
                        toolCalls[matching] = toolCalls[matching] with
                        {
                            Result = functionResult.Result?.ToString()
                        };
                    }
                }
            }

            yield return update;
        }

        if (reasoningStopwatch.IsRunning)
            reasoningStopwatch.Stop();

        if (sessionInfo != null)
        {
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.Assistant,
                Content = fullContent,
                ProviderId = sessionInfo.ProviderId,
                ModelName = sessionInfo.ModelName,
                ReasoningContent = string.IsNullOrEmpty(fullReasoning) ? null : fullReasoning,
                ReasoningDurationSeconds = reasoningStopwatch.Elapsed.TotalSeconds > 0
                    ? reasoningStopwatch.Elapsed.TotalSeconds
                    : null,
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null
            });
            sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;
        }
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
    private async IAsyncEnumerable<AgentResponseUpdate> StreamResponseOnlyAsync(
        string sessionId,
        string message,
        ChatSessionInfo sessionInfo,
        bool reasoningEnabled = false,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = true,
            ProviderId = sessionInfo.ProviderId,
            ReasoningEnabled = reasoningEnabled
        };

        var fullContent = string.Empty;
        var fullReasoning = string.Empty;
        var reasoningStopwatch = new Stopwatch();
        var toolCalls = new List<ToolCallInfo>();

        await foreach (var update in chatService.SendMessageStreamingAsync(request, ct))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextReasoningContent reasoning && !string.IsNullOrEmpty(reasoning.Text))
                {
                    if (!reasoningStopwatch.IsRunning)
                        reasoningStopwatch.Start();
                    fullReasoning += reasoning.Text;
                }
                else if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                {
                    if (reasoningStopwatch.IsRunning)
                        reasoningStopwatch.Stop();
                    fullContent += text.Text;
                }
                else if (content is FunctionCallContent functionCall)
                {
                    toolCalls.Add(new ToolCallInfo(
                        functionCall.Name,
                        functionCall.CallId ?? string.Empty,
                        functionCall.Arguments,
                        null,
                        DateTimeOffset.UtcNow));
                }
                else if (content is FunctionResultContent functionResult)
                {
                    var matching = toolCalls.FindIndex(t => t.CallId == functionResult.CallId);
                    if (matching >= 0)
                    {
                        toolCalls[matching] = toolCalls[matching] with
                        {
                            Result = functionResult.Result?.ToString()
                        };
                    }
                }
            }
            yield return update;
        }

        if (reasoningStopwatch.IsRunning)
            reasoningStopwatch.Stop();

        sessionInfo.Messages.Add(new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Content = fullContent,
            ProviderId = sessionInfo.ProviderId,
            ModelName = sessionInfo.ModelName,
            ReasoningContent = string.IsNullOrEmpty(fullReasoning) ? null : fullReasoning,
            ReasoningDurationSeconds = reasoningStopwatch.Elapsed.TotalSeconds > 0
                ? reasoningStopwatch.Elapsed.TotalSeconds
                : null,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null
        });
        sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;
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
