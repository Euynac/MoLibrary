using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Abstractions;
using Monica.AI.Chat.Models;
using Monica.AI.Chat.Services;
using Monica.AI.Models;
using Monica.AI.Models.Internal;
using Monica.AI.Services.Support;
using Monica.Modules;

namespace Monica.AI.Services;

/// <summary>
/// Stateless AI chat service backed by the Microsoft Agent Framework.
/// Creates and operates on ChatSession instances.
/// Session persistence and page state are handled outside the service.
/// </summary>
internal sealed class AIChatService(
    IAIProviderFactory providerFactory,
    IOptions<ModuleAIOption> options,
    IAIChatAgentFactory agentFactory,
    IAgentCapabilityStateStore capabilityStateStore,
    AgentStreamingCoordinator streamingCoordinator)
{
    private const string TOOL_APPROVAL_STATE_KEY = "toolApprovalState";
    private const string AUTO_APPROVED_FUNCTION_CALLS_STATE_KEY = "_autoApprovedFunctionCalls";
    private readonly ModuleAIOption _options = options.Value;

    /// <summary>
    /// Create a new chat session backed by ChatClientAgent.
    /// Registered tool providers can enrich the agent based on the session configuration.
    /// </summary>
    public async Task<ChatSession> CreateSessionAsync(
        string? providerId = null,
        string? modelName = null,
        string? systemPrompt = null,
        AIChatRuntimeContext? runtimeContext = null,
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
        var capabilityState = await capabilityStateStore.LoadAsync(ct);

        var runtime = await CreateAgentAsync(chatClient, resolvedPrompt, capabilityState, ct);
        var session = await runtime.Agent.CreateSessionAsync(ct);

        var state = new ChatSession(
            runtime,
            session,
            new ChatSessionSettings(
                resolvedProviderId,
                modelName,
                resolvedPrompt,
                reasoningEnabled),
            capabilityState.Revision,
            title ?? "New Chat",
            runtimeContext ?? AIChatRuntimeContext.Empty);

        return state;
    }

    /// <summary>Restores the visible transcript without constructing an agent runtime.</summary>
    public ChatSession RestoreSession(
        ChatSessionSnapshot snapshot,
        AIChatRuntimeContext? runtimeContext = null,
        string? expectedSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ChatSessionSnapshotValidator.Validate(snapshot, expectedSessionId);

        return new ChatSession(
            snapshot.SessionId,
            snapshot.Title,
            snapshot.CreatedAt,
            snapshot.UpdatedAt,
            snapshot.Settings,
            snapshot.Turns.Select(ChatSessionSnapshotMapper.FromSnapshot),
            snapshot.AgentSessionState,
            snapshot.AgentHistoryMessageCount,
            snapshot.Revision,
            runtimeContext ?? AIChatRuntimeContext.Empty);
    }

    /// <summary>Captures a complete durable snapshot without activating a lazy session.</summary>
    public async Task<ChatSessionSnapshot> CreateSnapshotAsync(
        ChatSession state,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        JsonElement? serializedAgentSession = state.SerializedAgentSession;
        if (state.IsRuntimeActive)
        {
            serializedAgentSession = await state.Agent.SerializeSessionAsync(
                state.AgentSession,
                cancellationToken: ct);
        }

        return new ChatSessionSnapshot
        {
            SessionId = state.SessionId,
            Title = state.Title,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt,
            Settings = state.Settings,
            Turns = state.Turns.Select(ChatSessionSnapshotMapper.ToSnapshot).ToArray(),
            AgentSessionState = RemovePendingApprovalState(serializedAgentSession),
            AgentHistoryMessageCount = state.MessageCount,
            Revision = state.PersistenceRevision
        };
    }

    /// <summary>Constructs and restores the private runtime on first use.</summary>
    public async Task ActivateSessionAsync(ChatSession state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsRuntimeActive)
        {
            return;
        }

        var provider = ResolveProvider(state.ProviderId);
        var capabilityState = await capabilityStateStore.LoadAsync(ct);
        var runtime = await CreateAgentAsync(
            provider.GetChatClient(state.ModelName),
            state.SystemPrompt,
            capabilityState,
            ct);

        try
        {
            var usedTranscriptFallback = false;
            var rebuiltFromTranscript = false;
            AgentSession agentSession;
            if (!state.NeedsRecreation && state.SerializedAgentSession is { } serializedState)
            {
                try
                {
                    agentSession = await runtime.Agent.DeserializeSessionAsync(
                        serializedState,
                        cancellationToken: ct);
                }
                catch (Exception ex) when (IsRecoverableSessionStateFailure(ex))
                {
                    agentSession = await CreateTranscriptFallbackSessionAsync(runtime.Agent, state, ct);
                    usedTranscriptFallback = true;
                    rebuiltFromTranscript = true;
                }
            }
            else
            {
                agentSession = await CreateTranscriptFallbackSessionAsync(runtime.Agent, state, ct);
                usedTranscriptFallback = state.SerializedAgentSession is null;
                rebuiltFromTranscript = true;
            }

            if (rebuiltFromTranscript)
            {
                state.RebaseHistoryCheckpointsForTranscript();
            }

            state.ActivateRuntime(
                runtime,
                agentSession,
                capabilityState.Revision,
                usedTranscriptFallback);
        }
        catch
        {
            await runtime.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Recreate the agent and session for the given state with current configuration.
    /// Preserves chat history by copying it to the new session.
    /// Resets the NeedsRecreation flag after completion.
    /// </summary>
    public async Task RecreateAgentAsync(ChatSession state, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(state.ProviderId);
        if (provider == null)
            throw new InvalidOperationException($"Provider '{state.ProviderId}' not found.");
        if (!provider.Info.IsValid)
            throw new InvalidOperationException(
                AIProviderAvailabilityMessages.BuildProviderUnavailableMessage(provider.Info));

        var chatClient = provider.GetChatClient(state.ModelName);
        var capabilityState = await capabilityStateStore.LoadAsync(ct);
        var newRuntime = await CreateAgentAsync(chatClient, state.SystemPrompt, capabilityState, ct);
        try
        {
            var oldHistory = state.ChatHistory is { } history ? history.ToList() : null;
            var newSession = await CreateReplacementSessionAsync(
                state.Agent,
                state.AgentSession,
                newRuntime.Agent,
                ct);
            CopyChatHistoryIfNeeded(newRuntime.Agent, newSession, oldHistory);
            await state.ReplaceRuntimeAsync(newRuntime, newSession, capabilityState.Revision);
        }
        catch
        {
            await newRuntime.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Send a message and get a streaming response via agent framework.
    /// Automatically recreates the agent if configuration has changed.
    /// </summary>
    public async IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        ChatSession state,
        string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in RunStreamingAsync(
                           state,
                           new ChatMessage(ChatRole.User, message),
                           ct))
        {
            yield return update;
        }
    }

    internal async IAsyncEnumerable<AgentResponseUpdate> ContinueApprovalStreamingAsync(
        ChatSession state,
        ToolApprovalResponseContent response,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in RunStreamingAsync(
                           state,
                           new ChatMessage(ChatRole.User, [response]),
                           ct))
        {
            yield return update;
        }
    }

    private async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ChatSession state,
        ChatMessage input,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await ActivateSessionAsync(state, ct);
        var capabilityState = await capabilityStateStore.LoadAsync(ct);
        if (state.CapabilityRevision != capabilityState.Revision)
        {
            state.MarkCapabilityRevision(capabilityState.Revision);
        }

        // Recreate agent if configuration changed
        if (state.NeedsRecreation)
        {
            await RecreateAgentAsync(state, ct);
        }

        var updateChannel = new AgentResponseUpdateChannel();
        var runOptions = CreateRunOptions(state, updateChannel);
        await foreach (var update in streamingCoordinator.RunAsync(
                           state.Agent,
                           state.AgentSession,
                           input,
                           state.RuntimeContext,
                           runOptions,
                           updateChannel,
                           ct))
        {
            yield return update;
        }

        state.MarkUpdated();
    }

    /// <summary>
    /// Send a message and get a non-streaming response.
    /// Automatically recreates the agent if configuration has changed.
    /// </summary>
    public async Task<string> SendMessageAsync(
        ChatSession state,
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
            if (provider == null)
            {
                throw new InvalidOperationException($"Provider '{providerId}' not found.");
            }

            if (!provider.Info.IsValid)
            {
                throw new InvalidOperationException(
                    AIProviderAvailabilityMessages.BuildProviderUnavailableMessage(provider.Info));
            }

            return provider;
        }

        provider = providerFactory.GetDefaultProvider();
        if (provider != null && provider.Info.IsValid) return provider;

        throw new InvalidOperationException(
            AIProviderAvailabilityMessages.BuildNoEnabledProviderMessage(providerFactory.GetAllProviderInfos()));
    }

    /// <summary>
    /// Creates a ChatClientAgent for the current session configuration.
    /// </summary>
    private async Task<AIChatAgentRuntime> CreateAgentAsync(
        IChatClient chatClient,
        string? instructions,
        AgentCapabilityState capabilityState,
        CancellationToken ct)
    {
        return await agentFactory.CreateAsync(
            chatClient,
            new AIChatAgentCreateContext
            {
                Instructions = instructions,
                CapabilityState = capabilityState
            },
            ct);
    }

    private ChatClientAgentRunOptions CreateRunOptions(
        ChatSession state,
        AgentResponseUpdateChannel updateChannel)
    {
        ArgumentNullException.ThrowIfNull(updateChannel);

        var runOptions = new ChatClientAgentRunOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary()
        };
        runOptions.AdditionalProperties.Add(updateChannel);

        if (state.ReasoningEnabled)
        {
            runOptions.ChatOptions = new ChatOptions
            {
                Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium }
            };
        }

        return runOptions;
    }

    private static async Task<AgentSession> CreateReplacementSessionAsync(
        AIAgent oldAgent,
        AgentSession oldSession,
        AIAgent newAgent,
        CancellationToken ct)
    {
        try
        {
            var serializedSession = await oldAgent.SerializeSessionAsync(
                oldSession,
                cancellationToken: ct);
            return await newAgent.DeserializeSessionAsync(
                serializedSession,
                cancellationToken: ct);
        }
        catch (ArgumentException)
        {
            return await newAgent.CreateSessionAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return await newAgent.CreateSessionAsync(ct);
        }
        catch (JsonException)
        {
            return await newAgent.CreateSessionAsync(ct);
        }
        catch (NotSupportedException)
        {
            return await newAgent.CreateSessionAsync(ct);
        }
    }

    private static void CopyChatHistoryIfNeeded(
        AIAgent newAgent,
        AgentSession newSession,
        IList<ChatMessage>? oldHistory)
    {
        if (oldHistory is not { Count: > 0 })
        {
            return;
        }

        var newProvider = newAgent.GetService<InMemoryChatHistoryProvider>();
        if (newProvider is null || newProvider.GetMessages(newSession)?.Count > 0)
        {
            return;
        }

        // Preserve local history when the serialized session did not carry it,
        // while keeping a deserialized Responses previous_response_id intact.
        newProvider.SetMessages(newSession, [.. oldHistory]);
    }

    private static async Task<AgentSession> CreateTranscriptFallbackSessionAsync(
        AIAgent agent,
        ChatSession state,
        CancellationToken ct)
    {
        var session = await agent.CreateSessionAsync(ct);
        var historyProvider = agent.GetService<InMemoryChatHistoryProvider>();
        if (historyProvider is not null)
        {
            historyProvider.SetMessages(
                session,
                state.Turns.SelectMany(static turn => GetVisibleHistoryMessages(turn)).ToList());
        }

        return session;
    }

    private static IEnumerable<ChatMessage> GetVisibleHistoryMessages(ChatTurn turn)
    {
        yield return turn.UserMessage.ToChatMessage();
        if (turn.AssistantMessage is not null)
        {
            yield return turn.AssistantMessage.ToChatMessage();
        }
    }

    private static bool IsRecoverableSessionStateFailure(Exception ex)
        => ex is ArgumentException
            or InvalidOperationException
            or JsonException
            or NotSupportedException;

    private static JsonElement? RemovePendingApprovalState(JsonElement? serializedState)
    {
        if (serializedState is not { ValueKind: JsonValueKind.Object } state)
        {
            return serializedState?.Clone();
        }

        var root = JsonNode.Parse(state.GetRawText()) as JsonObject;
        if (root?["stateBag"] is JsonObject stateBag)
        {
            stateBag.Remove(TOOL_APPROVAL_STATE_KEY);
            stateBag.Remove(AUTO_APPROVED_FUNCTION_CALLS_STATE_KEY);
        }

        return root is null ? state.Clone() : JsonSerializer.SerializeToElement(root);
    }
}
