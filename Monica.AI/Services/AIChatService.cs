using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Modules;
using Monica.AI.RAG.Services;

namespace Monica.AI.Services;

/// <summary>
/// AI chat service backed by the Microsoft Agent Framework.
/// Uses ChatClientAgent + AgentSession instead of custom IChatSession.
/// Optionally integrates RAG via TextSearchProvider when knowledge bases are specified.
/// </summary>
public class AIChatService(
    IAIProviderFactory providerFactory,
    IOptions<ModuleAIOption> options,
    IServiceProvider serviceProvider)
{
    private readonly ConcurrentDictionary<string, AgentSessionState> _sessions = new();
    private readonly ModuleAIOption _options = options.Value;
    private readonly RAGService? _ragService = serviceProvider.GetService(typeof(RAGService)) as RAGService;
    private readonly ILoggerFactory? _loggerFactory = serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

    /// <summary>
    /// Create a new chat session backed by ChatClientAgent.
    /// When knowledgeBaseIds are provided and RAG module is registered,
    /// the agent is created with a TextSearchProvider (AIContextProvider)
    /// that automatically performs RAG search on each invocation.
    /// </summary>
    public async Task<AgentSessionState> CreateSessionAsync(
        string? providerId = null,
        string? title = null,
        string? systemPrompt = null,
        IEnumerable<string>? knowledgeBaseIds = null,
        CancellationToken ct = default)
    {
        var provider = ResolveProvider(providerId);
        var resolvedProviderId = provider.ProviderId;

        var resolvedPrompt = systemPrompt;
        if (string.IsNullOrWhiteSpace(resolvedPrompt))
        {
            resolvedPrompt = provider.Info.SystemPrompt ?? _options.DefaultSystemPrompt;
        }

        var chatClient = provider.GetChatClient(null);
        var kbIds = knowledgeBaseIds?.ToList();

        var agent = CreateAgent(chatClient, resolvedPrompt, kbIds);

        var chatHistory = new InMemoryChatHistoryProvider();
        var session = await agent.CreateSessionAsync(chatHistory, ct);

        var state = new AgentSessionState(agent, session, chatHistory, resolvedProviderId)
        {
            Title = title ?? "New Chat",
            SystemPrompt = resolvedPrompt,
            ActiveKnowledgeBaseIds = kbIds
        };

        _sessions[state.SessionId] = state;
        return state;
    }

    /// <summary>
    /// Update session system prompt. Recreates the agent with new instructions,
    /// preserving RAG context provider if the session has active knowledge bases.
    /// </summary>
    public async Task<bool> UpdateSessionSystemPromptAsync(
        string sessionId,
        string? systemPrompt,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return false;

        state.SystemPrompt = systemPrompt;

        // Recreate agent with new instructions, reusing existing history
        var provider = providerFactory.GetProvider(state.ProviderId);
        if (provider == null) return false;

        var chatClient = provider.GetChatClient(state.ModelName);
        var newAgent = CreateAgent(chatClient, systemPrompt, state.ActiveKnowledgeBaseIds);
        var newSession = await newAgent.CreateSessionAsync(state.ChatHistory, ct);

        state.Agent = newAgent;
        state.Session = newSession;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Get a session by ID
    /// </summary>
    public AgentSessionState? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    /// <summary>
    /// Get or create a session
    /// </summary>
    public async Task<AgentSessionState> GetOrCreateSessionAsync(
        string? sessionId = null,
        string? providerId = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        return await CreateSessionAsync(providerId, ct: ct);
    }

    /// <summary>
    /// Get all sessions ordered by last update
    /// </summary>
    public IReadOnlyList<AgentSessionState> GetAllSessions()
    {
        return _sessions.Values.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Truncate session history to keep only the first N messages
    /// </summary>
    public bool TruncateSessionHistory(string sessionId, int keepCount)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
        {
            state.TruncateHistory(keepCount);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Send a message and get a non-streaming response
    /// </summary>
    public async Task<AIChatResponse> SendMessageAsync(AIChatRequest request, CancellationToken ct = default)
    {
        var state = await GetOrCreateSessionAsync(request.SessionId, request.ProviderId, ct);
        ApplyRequestOverrides(state, request);

        var userMessage = new ChatMessage(ChatRole.User, request.Message);

        var runOptions = CreateRunOptions(state, request.ReasoningEnabled);
        //TODO 解决上下文丢失问题：https://github.com/microsoft/agent-framework/pull/3798
        var response = await state.Agent.RunAsync([userMessage], state.Session, runOptions, ct);

        state.UpdatedAt = DateTimeOffset.UtcNow;

        // Auto-set title from first user message
        if (state.ChatHistory.Count(m => m.Role == ChatRole.User) == 1)
        {
            state.Title = request.Message.Length > 50 ? request.Message[..50] + "..." : request.Message;
        }

        var responseText = response.Text ?? string.Empty;
        var assistantMessage = new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Content = responseText,
            ProviderId = state.ProviderId,
            ModelName = state.ModelName
        };

        return new AIChatResponse
        {
            SessionId = state.SessionId,
            Message = assistantMessage,
            ProviderId = state.ProviderId,
            ModelName = state.ModelName
        };
    }

    /// <summary>
    /// Send a message and get a streaming response via agent framework
    /// </summary>
    public async IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var state = await GetOrCreateSessionAsync(request.SessionId, request.ProviderId, ct);
        ApplyRequestOverrides(state, request);

        var userMessage = new ChatMessage(ChatRole.User, request.Message);

        var runOptions = CreateRunOptions(state, request.ReasoningEnabled);

        await foreach (var update in state.Agent.RunStreamingAsync([userMessage], state.Session, runOptions, ct))
        {
            yield return update;
        }

        state.UpdatedAt = DateTimeOffset.UtcNow;

        // Auto-set title from first user message
        if (state.ChatHistory.Count(m => m.Role == ChatRole.User) == 1)
        {
            state.Title = request.Message.Length > 50 ? request.Message[..50] + "..." : request.Message;
        }
    }

    /// <summary>
    /// Get session ID for a request (creates session if needed)
    /// </summary>
    public async Task<string> GetSessionIdForRequestAsync(AIChatRequest request, CancellationToken ct = default)
    {
        var state = await GetOrCreateSessionAsync(request.SessionId, request.ProviderId, ct);
        return state.SessionId;
    }

    private IAIProvider ResolveProvider(string? providerId)
    {
        IAIProvider? provider;
        if (!string.IsNullOrEmpty(providerId))
        {
            provider = providerFactory.GetProvider(providerId);
            if (provider != null) return provider;
        }

        provider = providerFactory.GetDefaultProvider();
        if (provider != null) return provider;

        throw new InvalidOperationException("No AI provider available.");
    }

    /// <summary>
    /// Creates a ChatClientAgent, optionally with RAG TextSearchProvider integration.
    /// </summary>
    private ChatClientAgent CreateAgent(
        IChatClient chatClient,
        string? instructions,
        List<string>? knowledgeBaseIds)
    {
        if (_ragService is not null && knowledgeBaseIds is { Count: > 0 })
        {
            var ragOptions = serviceProvider.GetService(typeof(IOptions<ModuleRAGOption>))
                as IOptions<ModuleRAGOption>;
            var topK = ragOptions?.Value.DefaultTopK ?? 5;
            var searchProviderOptions = ragOptions?.Value.SearchProviderOptions;
            var searchAdapter = _ragService.CreateSearchAdapter(knowledgeBaseIds, topK);

            var agentOptions = new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions { Instructions = instructions },
                AIContextProviderFactory = (ctx, _) =>
                {
                    var provider = new TextSearchProvider(
                        searchAdapter,
                        ctx.SerializedState,
                        ctx.JsonSerializerOptions,
                        searchProviderOptions,
                        _loggerFactory);
                    return new ValueTask<AIContextProvider>(provider);
                }
            };

            return new ChatClientAgent(chatClient, agentOptions, _loggerFactory);
        }

        return new ChatClientAgent(chatClient, instructions: instructions);
    }

    private static void ApplyRequestOverrides(AgentSessionState state, AIChatRequest request)
    {
        if (!string.IsNullOrEmpty(request.ProviderId))
            state.ProviderId = request.ProviderId;

        if (!string.IsNullOrEmpty(request.ModelName))
            state.ModelName = request.ModelName;

        if (!string.IsNullOrEmpty(request.SystemPrompt))
            state.SystemPrompt = request.SystemPrompt;
    }

    private static ChatClientAgentRunOptions? CreateRunOptions(AgentSessionState state, bool reasoningEnabled)
    {
        if (!reasoningEnabled) return null;

        var chatOptions = new ChatOptions();
        chatOptions.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        chatOptions.AdditionalProperties["reasoning_effort"] = "medium";
        return new ChatClientAgentRunOptions { ChatOptions = chatOptions };
    }
}
