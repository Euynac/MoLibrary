using System.Text.Json;
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
    internal int HistoryCheckpoint { get; private set; }

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

    internal void RestoreErrors(IEnumerable<AIChatMessage> errors)
    {
        _errorMessages.AddRange(errors);
    }

    internal void RebaseHistoryCheckpoint(int historyCheckpoint)
    {
        HistoryCheckpoint = historyCheckpoint;
    }
}

/// <summary>Describes how a restored session's Agent Framework state was activated.</summary>
public enum ChatSessionRestorationState
{
    /// <summary>The session was created in the current process and did not require restoration.</summary>
    NotRequired,

    /// <summary>The visible transcript is loaded while runtime restoration remains deferred.</summary>
    Pending,

    /// <summary>The serialized Agent Framework session was restored successfully.</summary>
    Restored,

    /// <summary>Serialized state was unavailable or incompatible, so visible history was rebuilt.</summary>
    TranscriptFallback
}

/// <summary>
/// Owns the visible transcript and the private Agent Framework runtime for one conversation.
/// </summary>
public sealed class ChatSession : IAsyncDisposable
{
    private readonly List<ChatTurn> _turns = [];
    private readonly Dictionary<string, ToolApprovalRequestContent> _pendingApprovals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _approvalIdsByRequest = new(StringComparer.Ordinal);
    private AIChatAgentRuntime? _runtime;
    private AgentSession? _agentSession;
    private JsonElement? _serializedAgentSession;
    private int _historyMessageCount;
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
        RestorationState = ChatSessionRestorationState.NotRequired;
        _historyMessageCount = ChatHistory?.Count ?? 0;
    }

    internal ChatSession(
        string sessionId,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        ChatSessionSettings settings,
        IEnumerable<ChatTurn> turns,
        JsonElement? serializedAgentSession,
        int historyMessageCount,
        long persistenceRevision,
        AIChatRuntimeContext runtimeContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentOutOfRangeException.ThrowIfNegative(historyMessageCount);

        SessionId = sessionId;
        Title = title;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Settings = settings;
        RuntimeContext = runtimeContext;
        _turns.AddRange(turns);
        _serializedAgentSession = serializedAgentSession?.Clone();
        _historyMessageCount = historyMessageCount;
        PersistenceRevision = persistenceRevision;
        RestorationState = ChatSessionRestorationState.Pending;
    }

    /// <summary>Unique session identifier.</summary>
    public string SessionId { get; }

    /// <summary>Current display title.</summary>
    public string Title { get; private set; }

    /// <summary>Session creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Last transcript or settings update time.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Revision assigned by the configured history provider.</summary>
    public long PersistenceRevision { get; private set; }

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

    /// <summary>Current lazy runtime restoration state.</summary>
    public ChatSessionRestorationState RestorationState { get; private set; }

    /// <summary>Whether the private Agent Framework runtime has been activated.</summary>
    public bool IsRuntimeActive => _runtime is not null;

    /// <summary>Owned conversation turns in chronological order.</summary>
    public IReadOnlyList<ChatTurn> Turns => _turns;

    /// <summary>Flattened read-only transcript for presentation.</summary>
    public IReadOnlyList<AIChatMessage> Messages
        => _turns.SelectMany(static turn => turn.Messages).ToList();

    internal long CapabilityRevision => _capabilityRevision;

    internal bool NeedsRecreation => _needsRecreation;

    internal AIAgent Agent => _runtime?.Agent
        ?? throw new InvalidOperationException("The chat session runtime has not been activated.");

    internal AgentSession AgentSession => _agentSession
        ?? throw new InvalidOperationException("The chat session runtime has not been activated.");

    internal JsonElement? SerializedAgentSession => _serializedAgentSession?.Clone();

    internal IList<ChatMessage>? ChatHistory
        => _runtime?.Agent.GetService<InMemoryChatHistoryProvider>()?.GetMessages(_agentSession);

    internal int MessageCount => ChatHistory?.Count ?? _historyMessageCount;

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
        _serializedAgentSession = null;
        _historyMessageCount = ChatHistory?.Count ?? _historyMessageCount;
        _capabilityRevision = capabilityRevision;
        _needsRecreation = false;
        Touch();
        if (previousRuntime is not null)
        {
            await previousRuntime.DisposeAsync();
        }
    }

    internal void ActivateRuntime(
        AIChatAgentRuntime runtime,
        AgentSession agentSession,
        long capabilityRevision,
        bool usedTranscriptFallback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runtime is not null)
        {
            throw new InvalidOperationException("The chat session runtime is already active.");
        }

        _runtime = runtime;
        _agentSession = agentSession;
        _serializedAgentSession = null;
        _capabilityRevision = capabilityRevision;
        _needsRecreation = false;
        _historyMessageCount = ChatHistory?.Count ?? _historyMessageCount;
        RestorationState = usedTranscriptFallback
            ? ChatSessionRestorationState.TranscriptFallback
            : ChatSessionRestorationState.Restored;
    }

    internal void RebaseHistoryCheckpointsForTranscript()
    {
        var checkpoint = 0;
        foreach (var turn in _turns)
        {
            turn.RebaseHistoryCheckpoint(checkpoint);
            checkpoint++;
            if (turn.AssistantMessage is not null)
            {
                checkpoint++;
            }
        }

        _historyMessageCount = checkpoint;
    }

    internal void ClearHistory()
    {
        ChatHistory?.Clear();
        _serializedAgentSession = null;
        _historyMessageCount = 0;
        _turns.Clear();
        _pendingApprovals.Clear();
        _approvalIdsByRequest.Clear();
        Touch();
    }

    internal void MarkUpdated() => Touch();

    internal void MarkPersisted(long revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        PersistenceRevision = revision;
    }

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
        if (_runtime is null)
        {
            throw new InvalidOperationException("Activate the chat session runtime before editing history.");
        }

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
        _historyMessageCount = history?.Count ?? checkpoint;
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
        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
