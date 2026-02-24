using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using Monica.AI.Models;
using Monica.AI.RAG.Models;

namespace Monica.AI.UI.Models;

/// <summary>
/// Parameter object for ChatContainer component to reduce parameter count.
/// Consolidates 27 individual parameters into a single cohesive object.
/// </summary>
public sealed class ChatContainerParameters
{
    // Message data
    public required IReadOnlyList<AIChatMessage> Messages { get; init; }
    public IAsyncEnumerable<AgentResponseUpdate>? StreamingContent { get; init; }

    // State flags
    public bool IsSending { get; init; }
    public bool ShowRetry { get; init; }

    // Provider/Model info
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public bool ShowProviderInfo { get; init; }
    public IReadOnlyList<AIModelInfo>? AvailableModels { get; init; }

    // Error handling
    public string? ErrorMessage { get; init; }

    // Cancellation
    public CancellationToken CancellationToken { get; init; }

    // Features
    public bool EnableMarkdown { get; init; } = true;
    public bool EnableAutoScroll { get; init; } = true;
    public bool SupportsReasoning { get; init; }
    public bool ReasoningEnabled { get; init; }

    // RAG
    public IReadOnlyList<KnowledgeBase>? KnowledgeBases { get; init; }
    public List<string> SelectedKnowledgeBaseIds { get; init; } = [];

    // Event callbacks
    public required EventCallback<string> OnSendMessage { get; init; }
    public EventCallback<string> OnStreamComplete { get; init; }
    public EventCallback<string> OnStreamError { get; init; }
    public EventCallback OnCancel { get; init; }
    public EventCallback OnRetry { get; init; }
    public EventCallback OnErrorDismissed { get; init; }
    public EventCallback<(AIChatMessage Message, string NewContent)> OnEditMessage { get; init; }
    public EventCallback<AIChatMessage> OnRetryMessage { get; init; }
    public EventCallback<string> OnModelChanged { get; init; }
    public EventCallback<bool> ReasoningEnabledChanged { get; init; }
    public EventCallback<List<string>> SelectedKnowledgeBaseIdsChanged { get; init; }
}
