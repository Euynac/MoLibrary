using Microsoft.Agents.AI;
using Monica.AI.Models;
using Monica.AI.RAG.Models;
using Monica.AI.Services;

namespace Monica.AI.UI.Models;

/// <summary>
/// Encapsulates all state for the AI Chat page.
/// Reduces field clutter and improves state management clarity.
/// </summary>
public sealed class ChatPageState
{
    // Provider/Model state (UI-specific)
    public string CurrentProviderName { get; set; } = string.Empty;
    public IReadOnlyList<AIProviderInfo> Providers { get; set; } = [];
    public IReadOnlyList<AIModelInfo> CurrentProviderModels { get; set; } = [];

    // Default provider/model for initial session creation (before any session exists)
    public string? DefaultProviderId { get; set; }
    public string? DefaultModelName { get; set; }

    // Session state reference
    public AgentSessionState? CurrentAgentSessionState { get; set; }

    // Computed properties from session state (with fallback to defaults)
    public string? CurrentProviderId => CurrentAgentSessionState?.ProviderId ?? DefaultProviderId;
    public string? CurrentModelName => CurrentAgentSessionState?.ModelName ?? DefaultModelName;
    public string CurrentSessionTitle => CurrentAgentSessionState?.Title ?? "New Conversation";
    public IReadOnlyList<AIChatMessage> CurrentMessages => CurrentAgentSessionState?.Messages ?? (IReadOnlyList<AIChatMessage>)Array.Empty<AIChatMessage>();

    // UI state
    public bool IsSending { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CanRetry { get; set; }
    public string? LastMessage { get; set; }

    // Streaming state
    public IAsyncEnumerable<AgentResponseUpdate>? StreamingContent { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    public CancellationToken CancellationToken { get; set; }

    // Feature state
    public bool ReasoningEnabled { get; set; }
    public bool SupportsReasoning { get; set; }

    // RAG state
    public IReadOnlyList<KnowledgeBase> KnowledgeBases { get; set; } = [];
    public List<string> SelectedKnowledgeBaseIds { get; set; } = [];

    public void ClearError()
    {
        ErrorMessage = null;
        CanRetry = false;
    }

    public void SetError(string message, bool canRetry = false)
    {
        ErrorMessage = message;
        CanRetry = canRetry;
    }

    public void SetupCancellationToken(int timeoutMs = 0)
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
        CancellationToken = CancellationTokenSource.Token;

        if (timeoutMs > 0)
        {
            CancellationTokenSource.CancelAfter(timeoutMs);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource?.Dispose();
    }
}
