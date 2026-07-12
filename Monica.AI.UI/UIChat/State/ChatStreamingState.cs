using System.Diagnostics;
using Monica.AI.Chat.Models;
using Monica.AI.Models;

namespace Monica.AI.UI.UIChat.State;

/// <summary>
/// UI-owned projection of the active chat stream. Components render this state and never start
/// background stream-consumption work.
/// </summary>
public sealed class ChatStreamingState
{
    private readonly Stopwatch _reasoningStopwatch = new();
    private readonly List<ToolCallInfo> _toolCalls = [];

    /// <summary>Accumulated assistant text.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>Accumulated reasoning text.</summary>
    public string Reasoning { get; private set; } = string.Empty;

    /// <summary>Current tool-call snapshots.</summary>
    public IReadOnlyList<ToolCallInfo> ToolCalls => _toolCalls;

    /// <summary>Whether stream consumption is active.</summary>
    public bool IsStreaming { get; private set; } = true;

    /// <summary>Whether reasoning is currently being emitted.</summary>
    public bool IsReasoning => _reasoningStopwatch.IsRunning;

    /// <summary>Elapsed reasoning duration in seconds.</summary>
    public double ReasoningDurationSeconds => _reasoningStopwatch.Elapsed.TotalSeconds;

    /// <summary>Pending external-script approval, when the stream paused for user input.</summary>
    public ChatApprovalRequestEvent? PendingApproval { get; private set; }

    /// <summary>Applies one Monica-owned stream event.</summary>
    public void Apply(ChatStreamEvent streamEvent)
    {
        switch (streamEvent)
        {
            case ChatReasoningDeltaEvent reasoning:
                if (!_reasoningStopwatch.IsRunning)
                {
                    _reasoningStopwatch.Start();
                }

                Reasoning += reasoning.Text;
                break;
            case ChatTextDeltaEvent text:
                _reasoningStopwatch.Stop();
                Content += text.Text;
                break;
            case ChatToolEvent tool:
                ApplyToolEvent(tool);
                break;
            case ChatApprovalRequestEvent approval:
                PendingApproval = approval;
                break;
            case ChatCompletedEvent:
                _reasoningStopwatch.Stop();
                IsStreaming = false;
                break;
        }
    }

    /// <summary>Prepares the projection for an approval continuation stream.</summary>
    public void ResumeAfterApproval()
    {
        PendingApproval = null;
        IsStreaming = true;
    }

    private void ApplyToolEvent(ChatToolEvent tool)
    {
        var index = _toolCalls.FindIndex(call => call.CallId == tool.CallId);
        var existing = index >= 0 ? _toolCalls[index] : null;
        var now = DateTimeOffset.UtcNow;
        var status = tool.Status switch
        {
            ChatToolEventStatus.Started => ToolCallStatus.Running,
            ChatToolEventStatus.Completed => ToolCallStatus.Completed,
            ChatToolEventStatus.Failed => ToolCallStatus.Failed,
            _ => throw new InvalidOperationException($"Unknown tool event status '{tool.Status}'.")
        };

        var updated = new ToolCallInfo
        {
            ToolName = tool.ToolName,
            CallId = tool.CallId,
            ArgumentsText = tool.Arguments ?? existing?.ArgumentsText,
            ResultText = tool.Result ?? existing?.ResultText,
            ExceptionMessage = tool.Error ?? existing?.ExceptionMessage,
            Status = status,
            StartedAt = existing?.StartedAt ?? now,
            CompletedAt = status == ToolCallStatus.Running ? null : now
        };

        if (index >= 0)
        {
            _toolCalls[index] = updated;
        }
        else
        {
            _toolCalls.Add(updated);
        }
    }
}
