using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.AI.Abstractions;
using Monica.Modules;
using Monica.AI.RAG.Services;

namespace Monica.AI.Services;

/// <summary>
/// Stateless AI chat service backed by the Microsoft Agent Framework.
/// Creates and operates on AgentSessionState instances.
/// Session management is handled by the UI service layer.
/// </summary>
public class AIChatService(
    IAIProviderFactory providerFactory,
    IOptions<ModuleAIOption> options,
    IServiceProvider serviceProvider)
{
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
        string? modelName = null,
        string? systemPrompt = null,
        List<string>? knowledgeBaseIds = null,
        bool reasoningEnabled = false,
        string? title = null,
        CancellationToken ct = default)
    {
        var provider = ResolveProvider(providerId);
        var resolvedProviderId = provider.ProviderId;

        var resolvedPrompt = systemPrompt;
        if (string.IsNullOrWhiteSpace(resolvedPrompt))
        {
            resolvedPrompt = provider.Info.SystemPrompt ?? _options.DefaultSystemPrompt;
        }

        var chatClient = provider.GetChatClient(modelName);

        var agent = CreateAgent(chatClient, resolvedPrompt, knowledgeBaseIds);
        var session = await agent.CreateSessionAsync(ct);

        var state = new AgentSessionState(agent, session, resolvedProviderId)
        {
            Title = title ?? "New Chat",
            SystemPrompt = resolvedPrompt,
            ActiveKnowledgeBaseIds = knowledgeBaseIds,
            ModelName = modelName,
            ReasoningEnabled = reasoningEnabled
        };

        return state;
    }

    /// <summary>
    /// Recreate the agent and session for the given state with current configuration.
    /// Preserves chat history by copying it to the new session.
    /// Resets the NeedsRecreation flag after completion.
    /// </summary>
    public async Task RecreateAgentAsync(AgentSessionState state, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(state.ProviderId);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{state.ProviderId}' not found.");

        var chatClient = provider.GetChatClient(state.ModelName);
        var newAgent = CreateAgent(chatClient, state.SystemPrompt, state.ActiveKnowledgeBaseIds);
        var newSession = await newAgent.CreateSessionAsync(ct);

        // Copy chat history from old session to new session
        var oldHistory = state.ChatHistory;
        if (oldHistory != null && oldHistory.Count > 0)
        {
            var newProvider = newAgent.GetService<InMemoryChatHistoryProvider>();
            if (newProvider != null)
            {
                // Initialize the new session's state by setting messages directly
                newProvider.SetMessages(newSession, new List<ChatMessage>(oldHistory));
            }
        }

        state.Agent = newAgent;
        state.Session = newSession;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        state.ResetRecreationFlag();
    }

    /// <summary>
    /// Send a message and get a streaming response via agent framework.
    /// Automatically recreates the agent if configuration has changed.
    /// </summary>
    public async IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        AgentSessionState state,
        string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Recreate agent if configuration changed
        if (state.NeedsRecreation)
        {
            await RecreateAgentAsync(state, ct);
        }

        var userMessage = new ChatMessage(ChatRole.User, message);
        var runOptions = CreateRunOptions(state);

        await foreach (var update in state.Agent.RunStreamingAsync([userMessage], state.Session, runOptions, ct))
        {
            yield return update;
        }

        state.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Send a message and get a non-streaming response.
    /// Automatically recreates the agent if configuration has changed.
    /// </summary>
    public async Task<string> SendMessageAsync(
        AgentSessionState state,
        string message,
        CancellationToken ct = default)
    {
        var fullContent = string.Empty;

        await foreach (var update in SendMessageStreamingAsync(state, message, ct))
        {
            foreach (var content in update.Contents)
            {
                if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                {
                    fullContent += text.Text;
                }
            }
        }

        return fullContent;
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

            var textSearchProvider = new TextSearchProvider(searchAdapter, options: searchProviderOptions);

            var agentOptions = new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions { Instructions = instructions },
                AIContextProviders = new List<AIContextProvider> { textSearchProvider }
            };

            return new ChatClientAgent(chatClient, agentOptions, _loggerFactory);
        }

        return new ChatClientAgent(chatClient, instructions: instructions);
    }

    private static ChatClientAgentRunOptions? CreateRunOptions(AgentSessionState state)
    {
        if (!state.ReasoningEnabled) return null;

        var chatOptions = new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium }
        };
        return new ChatClientAgentRunOptions { ChatOptions = chatOptions };
    }
}
