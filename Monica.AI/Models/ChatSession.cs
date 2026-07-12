using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Models.Internal;
using Monica.AI.Services.Support;

namespace Monica.AI.Models;

/// <summary>
/// Immutable configuration used to compose a chat session runtime.
/// </summary>
/// <param name="ProviderId">Provider identifier used by the session.</param>
/// <param name="ModelName">Optional provider model name.</param>
/// <param name="SystemPrompt">Optional instructions supplied to the agent.</param>
/// <param name="ReasoningEnabled">Whether reasoning is requested for each turn.</param>
public sealed record ChatSessionSettings(
    string ProviderId,
    string? ModelName,
    string? SystemPrompt,
    bool ReasoningEnabled);

/// <summary>
/// One user request and its optional assistant response.
/// </summary>
/// <remarks>
/// The history checkpoint records the Agent Framework history size before the turn. This keeps
/// edit and retry behavior correct when one visible turn contains reasoning or multiple tool messages.
/// </remarks>
public sealed record ChatTurn
{
    private readonly List<AIChatMessage> _errorMessages = [];

    internal ChatTurn(AIChatMessage userMessage, int historyCheckpoint)
    {
        UserMessage = userMessage;
        HistoryCheckpoint = historyCheckpoint;
    }

    /// <summary>Message submitted by the user.</summary>
    public AIChatMessage UserMessage { get; }

    /// <summary>Assistant response committed for the turn, when available.</summary>
    public AIChatMessage? AssistantMessage { get; internal set; }

    /// <summary>Generation failures retained after any partial assistant response.</summary>
    public IReadOnlyList<AIChatMessage> ErrorMessages => _errorMessages;

    /// <summary>Agent history size before this turn started.</summary>
    internal int HistoryCheckpoint { get; }

    internal IEnumerable<AIChatMessage> Messages
    {
        get
        {
            yield return UserMessage;
            if (AssistantMessage is not null)
            {
                yield return AssistantMessage;
            }

            foreach (var errorMessage in _errorMessages)
            {
                yield return errorMessage;
            }
        }
    }

    internal bool AddError(string error)
    {
        if (_errorMessages.LastOrDefault()?.Content == error)
        {
            return false;
        }

        _errorMessages.Add(new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Kind = AIChatMessageKind.Error,
            Content = error
        });
        return true;
    }
}

/// <summary>
/// Owns the visible transcript and the private Agent Framework runtime for one conversation.
/// </summary>
public sealed class ChatSession : IAsyncDisposable
{
    private readonly List<ChatTurn> _turns = [];
    private readonly Dictionary<string, ToolApprovalRequestContent> _pendingApprovals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _approvalIdsByRequest = new(StringComparer.Ordinal);
    private AIChatAgentRuntime _runtime;
    private AgentSession _agentSession;
    private long _capabilityRevision;
    private bool _needsRecreation;
    private bool _disposed;

    internal ChatSession(
        AIChatAgentRuntime runtime,
        AgentSession agentSession,
        ChatSessionSettings settings,
        long capabilityRevision,
        string title,
        AIChatRuntimeContext runtimeContext)
    {
        _runtime = runtime;
        _agentSession = agentSession;
        Settings = settings;
        _capabilityRevision = capabilityRevision;
        Title = title;
        RuntimeContext = runtimeContext;
        SessionId = Guid.NewGuid().ToString("N");
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Unique session identifier.</summary>
    public string SessionId { get; }

    /// <summary>Current display title.</summary>
    public string Title { get; private set; }

    /// <summary>Session creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Last transcript or settings update time.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Current read-only session configuration.</summary>
    public ChatSessionSettings Settings { get; private set; }

    /// <summary>Current provider identifier.</summary>
    public string ProviderId => Settings.ProviderId;

    /// <summary>Current model name.</summary>
    public string? ModelName => Settings.ModelName;

    /// <summary>Current system prompt.</summary>
    public string? SystemPrompt => Settings.SystemPrompt;

    /// <summary>Whether reasoning is enabled for each turn.</summary>
    public bool ReasoningEnabled => Settings.ReasoningEnabled;

    /// <summary>Runtime context made available to tools during an invocation.</summary>
    public AIChatRuntimeContext RuntimeContext { get; private set; }

    /// <summary>Owned conversation turns in chronological order.</summary>
    public IReadOnlyList<ChatTurn> Turns => _turns;

    /// <summary>Flattened read-only transcript for presentation.</summary>
    public IReadOnlyList<AIChatMessage> Messages
        => _turns.SelectMany(static turn => turn.Messages).ToList();

    internal long CapabilityRevision => _capabilityRevision;

    internal bool NeedsRecreation => _needsRecreation;

    internal AIAgent Agent => _runtime.Agent;

    internal AgentSession AgentSession => _agentSession;

    internal IList<ChatMessage>? ChatHistory
        => Agent.GetService<InMemoryChatHistoryProvider>()?.GetMessages(_agentSession);

    internal int MessageCount => ChatHistory?.Count ?? 0;

    internal void ApplySettings(ChatSessionSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Settings == settings)
        {
            return;
        }

        var runtimeChanged = !string.Equals(Settings.ProviderId, settings.ProviderId, StringComparison.Ordinal)
                             || !string.Equals(Settings.ModelName, settings.ModelName, StringComparison.Ordinal)
                             || !string.Equals(Settings.SystemPrompt, settings.SystemPrompt, StringComparison.Ordinal);
        Settings = settings;
        _needsRecreation |= runtimeChanged;
        Touch();
    }

    internal void Rename(string title)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Title = title;
        Touch();
    }

    internal void SetRuntimeContext(AIChatRuntimeContext runtimeContext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RuntimeContext = runtimeContext;
    }

    internal ChatTurn BeginTurn(string content)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var turn = new ChatTurn(
            new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = content
            },
            MessageCount);
        _turns.Add(turn);
        Touch();
        return turn;
    }

    internal ChatTurn GetOpenTurn()
        => _turns.LastOrDefault()?.AssistantMessage is null && _turns.Count > 0
            ? _turns[^1]
            : throw new InvalidOperationException("The session has no turn awaiting continuation.");

    internal void CompleteTurn(ChatTurn turn, AIChatMessage assistantMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_turns.Contains(turn))
        {
            throw new InvalidOperationException("The chat turn no longer belongs to this session.");
        }

        turn.AssistantMessage = assistantMessage;
        Touch();
    }

    internal void RecordError(ChatTurn turn, string error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_turns.Contains(turn))
        {
            throw new InvalidOperationException("The chat turn no longer belongs to this session.");
        }

        if (turn.AddError(error))
        {
            Touch();
        }
    }

    internal void RecordError(string error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var turn = _turns.LastOrDefault()
                   ?? throw new InvalidOperationException("The session has no turn for the error entry.");
        if (turn.AddError(error))
        {
            Touch();
        }
    }

    internal string RewindForEdit(string messageId, string newContent)
    {
        var index = _turns.FindIndex(turn => turn.UserMessage.Id == messageId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"User message '{messageId}' was not found.");
        }

        Rewind(index);
        return newContent;
    }

    internal string RewindForRetry(string messageId)
    {
        var index = _turns.FindIndex(turn =>
            turn.AssistantMessage?.Id == messageId
            || turn.ErrorMessages.Any(error => error.Id == messageId));
        if (index < 0)
        {
            throw new KeyNotFoundException($"Assistant message '{messageId}' was not found.");
        }

        var content = _turns[index].UserMessage.Content;
        Rewind(index);
        return content;
    }

    internal void MarkCapabilityRevision(long capabilityRevision)
    {
        if (_capabilityRevision == capabilityRevision)
        {
            return;
        }

        _capabilityRevision = capabilityRevision;
        _needsRecreation = true;
    }

    internal async ValueTask ReplaceRuntimeAsync(
        AIChatAgentRuntime runtime,
        AgentSession agentSession,
        long capabilityRevision)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previousRuntime = _runtime;
        _runtime = runtime;
        _agentSession = agentSession;
        _capabilityRevision = capabilityRevision;
        _needsRecreation = false;
        Touch();
        await previousRuntime.DisposeAsync();
    }

    internal void ClearHistory()
    {
        ChatHistory?.Clear();
        _turns.Clear();
        Touch();
    }

    internal void MarkUpdated() => Touch();

    internal string StorePendingApproval(ToolApprovalRequestContent request)
    {
        if (_approvalIdsByRequest.TryGetValue(request.RequestId, out var existingId))
        {
            return existingId;
        }

        var approvalId = Guid.NewGuid().ToString("N");
        _pendingApprovals.Add(approvalId, request);
        _approvalIdsByRequest.Add(request.RequestId, approvalId);
        return approvalId;
    }

    internal ToolApprovalRequestContent TakePendingApproval(string approvalId)
    {
        if (!_pendingApprovals.Remove(approvalId, out var request))
        {
            throw new KeyNotFoundException($"Approval request '{approvalId}' was not found or already resolved.");
        }

        _approvalIdsByRequest.Remove(request.RequestId);
        return request;
    }

    private void Rewind(int turnIndex)
    {
        var checkpoint = _turns[turnIndex].HistoryCheckpoint;
        var history = ChatHistory;
        if (history is not null)
        {
            while (history.Count > checkpoint)
            {
                history.RemoveAt(history.Count - 1);
            }
        }

        _turns.RemoveRange(turnIndex, _turns.Count - turnIndex);
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingApprovals.Clear();
        _approvalIdsByRequest.Clear();
        await _runtime.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
