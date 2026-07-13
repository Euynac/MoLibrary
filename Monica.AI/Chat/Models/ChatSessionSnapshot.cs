using System.Text.Json;
using Monica.AI.Models;

namespace Monica.AI.Chat.Models;

/// <summary>
/// Immutable durable representation of a Monica chat session.
/// </summary>
/// <remarks>
/// <see cref="AgentSessionState"/> is opaque Agent Framework state and can contain sensitive
/// prompts, tool payloads, and provider identifiers. Providers are responsible for securing it.
/// Pending approval objects are intentionally not part of this contract.
/// </remarks>
public sealed record ChatSessionSnapshot
{
    private IReadOnlyList<ChatTurnSnapshot> _turns = Array.Empty<ChatTurnSnapshot>();
    private JsonElement? _agentSessionState;

    /// <summary>Current serialized contract version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Contract version used to create this snapshot.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Unique session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Display title.</summary>
    public required string Title { get; init; }

    /// <summary>Session creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last transcript or settings update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Read-only settings needed to rebuild the agent runtime.</summary>
    public required ChatSessionSettings Settings { get; init; }

    /// <summary>Owned conversation turns, including checkpoints and error entries.</summary>
    public required IReadOnlyList<ChatTurnSnapshot> Turns
    {
        get => _turns;
        init => _turns = ChatSnapshotOwnership.Own(value);
    }

    /// <summary>Opaque state produced by <c>AIAgent.SerializeSessionAsync</c>, when available.</summary>
    public JsonElement? AgentSessionState
    {
        get => _agentSessionState?.Clone();
        init => _agentSessionState = value?.Clone();
    }

    /// <summary>Agent Framework history size associated with the serialized state.</summary>
    public int AgentHistoryMessageCount { get; init; }

    /// <summary>Revision assigned by the persistence provider.</summary>
    public long Revision { get; init; }

    /// <summary>Creates the catalog representation of this snapshot.</summary>
    public ChatSessionSummary ToSummary() => new()
    {
        SessionId = SessionId,
        Title = Title,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Settings = Settings,
        Revision = Revision
    };
}

/// <summary>Immutable durable representation of one conversation turn.</summary>
public sealed record ChatTurnSnapshot
{
    private IReadOnlyList<ChatMessageSnapshot> _errorMessages = Array.Empty<ChatMessageSnapshot>();

    /// <summary>User message that opened the turn.</summary>
    public required ChatMessageSnapshot UserMessage { get; init; }

    /// <summary>Committed assistant response, when generation produced one.</summary>
    public ChatMessageSnapshot? AssistantMessage { get; init; }

    /// <summary>Generation errors retained after the optional assistant response.</summary>
    public IReadOnlyList<ChatMessageSnapshot> ErrorMessages
    {
        get => _errorMessages;
        init => _errorMessages = ChatSnapshotOwnership.Own(value);
    }

    /// <summary>Agent Framework history size before the turn started.</summary>
    public int HistoryCheckpoint { get; init; }
}

/// <summary>Immutable durable representation of a visible chat message.</summary>
public sealed record ChatMessageSnapshot
{
    private IReadOnlyList<ChatRequestUsageSnapshot> _requestUsages = Array.Empty<ChatRequestUsageSnapshot>();
    private IReadOnlyList<ChatToolCallSnapshot> _toolCalls = Array.Empty<ChatToolCallSnapshot>();

    /// <summary>Unique message identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Message role.</summary>
    public AIChatRole Role { get; init; }

    /// <summary>Presentation kind.</summary>
    public AIChatMessageKind Kind { get; init; }

    /// <summary>Visible text content.</summary>
    public required string Content { get; init; }

    /// <summary>Message creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Provider model used for this message.</summary>
    public string? ModelName { get; init; }

    /// <summary>Provider identifier used for this message.</summary>
    public string? ProviderId { get; init; }

    /// <summary>Aggregate token usage, when reported.</summary>
    public ChatTokenUsageSnapshot? Usage { get; init; }

    /// <summary>Per-provider-call token usage.</summary>
    public IReadOnlyList<ChatRequestUsageSnapshot> RequestUsages
    {
        get => _requestUsages;
        init => _requestUsages = ChatSnapshotOwnership.Own(value);
    }

    /// <summary>Reasoning text emitted by the model.</summary>
    public string? ReasoningContent { get; init; }

    /// <summary>Reasoning duration in seconds.</summary>
    public double? ReasoningDurationSeconds { get; init; }

    /// <summary>Tool calls made while producing the message.</summary>
    public IReadOnlyList<ChatToolCallSnapshot> ToolCalls
    {
        get => _toolCalls;
        init => _toolCalls = ChatSnapshotOwnership.Own(value);
    }
}

/// <summary>Immutable durable token usage values.</summary>
/// <param name="InputTokens">Input tokens reported by the provider.</param>
/// <param name="OutputTokens">Output tokens reported by the provider.</param>
/// <param name="ReasoningTokens">Reasoning tokens included in the output total.</param>
/// <param name="CachedInputTokens">Input tokens served from the provider cache.</param>
/// <param name="TotalTokens">Provider-reported total token count.</param>
public sealed record ChatTokenUsageSnapshot(
    int InputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int CachedInputTokens,
    int TotalTokens);

/// <summary>Immutable durable token usage for one provider request.</summary>
/// <param name="Sequence">One-based provider request order.</param>
/// <param name="CreatedAt">Time when usage was observed.</param>
/// <param name="ResponseId">Provider response identifier, when available.</param>
/// <param name="MessageId">Provider message identifier, when available.</param>
/// <param name="Usage">Token usage reported for the request.</param>
public sealed record ChatRequestUsageSnapshot(
    int Sequence,
    DateTimeOffset CreatedAt,
    string? ResponseId,
    string? MessageId,
    ChatTokenUsageSnapshot Usage);

/// <summary>Immutable durable representation of one tool invocation.</summary>
public sealed record ChatToolCallSnapshot
{
    private JsonElement? _arguments;

    /// <summary>Tool name reported by the model.</summary>
    public required string ToolName { get; init; }

    /// <summary>Provider tool-call identifier.</summary>
    public required string CallId { get; init; }

    /// <summary>Structured tool arguments, when serializable.</summary>
    public JsonElement? Arguments
    {
        get => _arguments?.Clone();
        init => _arguments = value?.Clone();
    }

    /// <summary>Display-ready serialized arguments.</summary>
    public string? ArgumentsText { get; init; }

    /// <summary>Display-ready serialized result.</summary>
    public string? ResultText { get; init; }

    /// <summary>Exception text when execution failed.</summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>Tool execution status.</summary>
    public ToolCallStatus Status { get; init; }

    /// <summary>Invocation start time.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Invocation completion time.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

internal static class ChatSnapshotOwnership
{
    public static IReadOnlyList<T> Own<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Array.AsReadOnly(values.ToArray());
    }
}
