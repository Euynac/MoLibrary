using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Services.Support;
using Monica.Modules;

namespace Monica.AI.Services;

/// <summary>
/// Stateless AI chat service backed by the Microsoft Agent Framework.
/// Creates and operates on ChatSession instances.
/// Session persistence and page state are handled outside the service.
/// </summary>
public class AIChatService(
    IAIProviderFactory providerFactory,
    IOptions<ModuleAIOption> options,
    IAIChatAgentFactory agentFactory,
    IAgentCapabilityStateStore capabilityStateStore,
    Monica.AI.Skills.Services.MonicaSkillCatalog skillCatalog,
    Monica.AI.Mcp.Services.MonicaMcpCatalog mcpCatalog,
    AIChatRuntimeContextAccessor runtimeContextAccessor)
{
    private readonly ModuleAIOption _options = options.Value;
    private readonly AIChatRuntimeContextAccessor _runtimeContextAccessor = runtimeContextAccessor;

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

        var agent = await CreateAgentAsync(chatClient, resolvedPrompt, capabilityState, ct);
        var session = await agent.CreateSessionAsync(ct);

        var state = new ChatSession(agent, session, resolvedProviderId, capabilityState.Revision)
        {
            Title = title ?? "New Chat",
            SystemPrompt = resolvedPrompt,
            RuntimeContext = runtimeContext ?? AIChatRuntimeContext.Empty,
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
        var newAgent = await CreateAgentAsync(chatClient, state.SystemPrompt, capabilityState, ct);
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
        state.CapabilityRevision = capabilityState.Revision;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        state.ResetRecreationFlag();
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

        var userMessage = await CreateUserMessageAsync(message, state.RuntimeContext, capabilityState, ct);
        var updateChannel = new AgentResponseUpdateChannel();
        var runOptions = await CreateRunOptionsAsync(state, updateChannel, ct);
        _ = ProduceStreamingUpdatesAsync(state, userMessage, runOptions, updateChannel, ct);

        await foreach (var update in updateChannel.ReadAllAsync(ct))
        {
            yield return update;
        }
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
    private async Task<AIAgent> CreateAgentAsync(
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

    private async Task<ChatClientAgentRunOptions> CreateRunOptionsAsync(
        ChatSession state,
        AgentResponseUpdateChannel updateChannel,
        CancellationToken ct)
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

        var referenceInstructions = await BuildReferenceInstructionsAsync(state.RuntimeContext, ct);
        if (!string.IsNullOrWhiteSpace(referenceInstructions))
        {
            runOptions.ChatOptions ??= new ChatOptions();
            runOptions.ChatOptions.Instructions = string.IsNullOrWhiteSpace(runOptions.ChatOptions.Instructions)
                ? referenceInstructions
                : runOptions.ChatOptions.Instructions + Environment.NewLine + Environment.NewLine + referenceInstructions;
        }

        return runOptions;
    }

    private async Task<ChatMessage> CreateUserMessageAsync(
        string message,
        AIChatRuntimeContext runtimeContext,
        AgentCapabilityState capabilityState,
        CancellationToken ct)
    {
        var references = runtimeContext.GetOrDefault(AgentCapabilityChatRuntimeContextKeys.References);
        if (references is not { Count: > 0 })
        {
            return new ChatMessage(ChatRole.User, message);
        }

        var enabledReferences = await ResolveEnabledReferencesAsync(references, capabilityState, ct);
        if (enabledReferences.Count == 0)
        {
            return new ChatMessage(ChatRole.User, message);
        }

        var referenceLine = "Explicit Monica capability references: " +
                            string.Join(", ", enabledReferences.Select(static reference => $"{reference.Kind}:{reference.Name}"));
        return new ChatMessage(ChatRole.User, message + Environment.NewLine + Environment.NewLine + referenceLine);
    }

    private async Task<string?> BuildReferenceInstructionsAsync(
        AIChatRuntimeContext runtimeContext,
        CancellationToken ct)
    {
        var references = runtimeContext.GetOrDefault(AgentCapabilityChatRuntimeContextKeys.References);
        if (references is not { Count: > 0 })
        {
            return null;
        }

        var capabilityState = await capabilityStateStore.LoadAsync(ct);
        var entries = skillCatalog.GetEntries(capabilityState)
            .Concat(await mcpCatalog.GetCapabilityEntriesAsync(capabilityState, ct))
            .ToList();
        return AgentCapabilityPromptBuilder.BuildReferenceInstructions(references, entries);
    }

    private async Task<IReadOnlyList<AgentCapabilityReference>> ResolveEnabledReferencesAsync(
        IReadOnlyList<AgentCapabilityReference> references,
        AgentCapabilityState capabilityState,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var available = skillCatalog.GetEntries(capabilityState)
            .Concat(await mcpCatalog.GetCapabilityEntriesAsync(capabilityState, ct))
            .Where(static entry => entry.IsEnabled)
            .Select(static entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return references
            .Where(reference => available.Contains(reference.Key))
            .ToList();
    }

    private async Task ProduceStreamingUpdatesAsync(
        ChatSession state,
        ChatMessage userMessage,
        ChatClientAgentRunOptions runOptions,
        AgentResponseUpdateChannel updateChannel,
        CancellationToken ct)
    {
        try
        {
            using (AgentResponseUpdateChannelContext.Push(updateChannel))
            using (_runtimeContextAccessor.Push(state.RuntimeContext))
            {
                await foreach (var update in state.Agent.RunStreamingAsync([userMessage], state.Session, runOptions, ct))
                {
                    await updateChannel.PublishAsync(update, ct);
                }
            }

            state.UpdatedAt = DateTimeOffset.UtcNow;
            updateChannel.Complete();
        }
        catch (Exception ex)
        {
            updateChannel.Complete(ex);
        }
    }
}
