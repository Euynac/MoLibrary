namespace Monica.AI.Chat.Models;

/// <summary>
/// Base type for Monica-owned chat streaming events. Consumers do not depend on Agent Framework
/// update types and can evolve independently of SDK changes.
/// </summary>
public abstract record ChatStreamEvent;

/// <summary>Incremental assistant text.</summary>
public sealed record ChatTextDeltaEvent(string Text) : ChatStreamEvent;

/// <summary>Incremental model reasoning text.</summary>
public sealed record ChatReasoningDeltaEvent(string Text) : ChatStreamEvent;

/// <summary>Lifecycle event for one tool invocation.</summary>
public sealed record ChatToolEvent(
    string ToolName,
    string CallId,
    ChatToolEventStatus Status,
    string? Arguments,
    string? Result,
    string? Error) : ChatStreamEvent;

/// <summary>Approval required before an external script can execute.</summary>
public sealed record ChatApprovalRequestEvent(
    string ApprovalId,
    string SkillName,
    string ScriptName,
    string Source,
    string Arguments) : ChatStreamEvent;

/// <summary>Signals that the current stream ended successfully or was cancelled normally.</summary>
public sealed record ChatCompletedEvent(bool WasCancelled, bool AwaitingApproval) : ChatStreamEvent;

/// <summary>Tool invocation phase represented by a <see cref="ChatToolEvent"/>.</summary>
public enum ChatToolEventStatus
{
    /// <summary>The tool invocation started.</summary>
    Started,

    /// <summary>The tool invocation completed successfully.</summary>
    Completed,

    /// <summary>The tool invocation failed.</summary>
    Failed
}
