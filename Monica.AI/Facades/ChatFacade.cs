using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Abstractions;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.Facades;

/// <summary>
/// Host-facing facade for chat session creation and streaming operations.
/// </summary>
public sealed class ChatFacade
{
    private readonly AIChatService _chatService;
    private readonly IAIProviderFactory _providerFactory;

    internal ChatFacade(AIChatService chatService, IAIProviderFactory providerFactory)
    {
        _chatService = chatService;
        _providerFactory = providerFactory;
    }
    /// <summary>
    /// Get all provider info
    /// </summary>
    public IReadOnlyList<AIProviderInfo> GetProviders()
    {
        return _providerFactory.GetAllProviderInfos();
    }

    /// <summary>
    /// Get default provider
    /// </summary>
    public AIProviderInfo? GetDefaultProvider()
    {
        return _providerFactory.GetDefaultProvider()?.Info;
    }

    /// <summary>
    /// Replaces a session's read-only settings. Provider, model, and prompt changes recreate the
    /// private agent runtime on the next turn; reasoning changes are applied per request.
    /// </summary>
    public Res UpdateSettings(ChatSession session, ChatSessionSettings settings)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(settings);
            session.ApplySettings(settings);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Renames a chat session.</summary>
    public Res Rename(ChatSession session, string title)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(session);
            session.Rename(title);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>Updates the tool runtime context used by subsequent turns.</summary>
    public Res UpdateRuntimeContext(ChatSession session, AIChatRuntimeContext runtimeContext)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(runtimeContext);
            session.SetRuntimeContext(runtimeContext);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Create a new session with the specified configuration.
    /// </summary>
    public async Task<Res<ChatSession>> CreateSessionAsync(
        string? providerId = null,
        string? modelName = null,
        string? systemPrompt = null,
        AIChatRuntimeContext? runtimeContext = null,
        bool reasoningEnabled = false,
        string? title = null,
        CancellationToken ct = default)
    {
        try
        {
            var state = await _chatService.CreateSessionAsync(
                providerId,
                modelName,
                systemPrompt,
                runtimeContext,
                reasoningEnabled,
                title,
                ct);

            return state;
        }
        catch (NotSupportedException ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to create chat session: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Sends a message through one chat session and returns Monica-owned stream events.
    /// </summary>
    public IAsyncEnumerable<Res<ChatStreamEvent>> SendMessageStreamingAsync(
        ChatSession state,
        string message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ProcessStreamAsync(state, message, approvalResponse: null, ct);
    }

    /// <summary>
    /// Unified stream processing method that accumulates content, reasoning, and tool calls.
    /// </summary>
    private async IAsyncEnumerable<Res<ChatStreamEvent>> ProcessStreamAsync(
        ChatSession state,
        string? message,
        ToolApprovalResponseContent? approvalResponse,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var accumulator = new StreamingContentAccumulator();
        ChatTurn? turn = null;
        string? activationFailure = null;
        var activationCancelled = false;
        try
        {
            await _chatService.ActivateSessionAsync(state, ct);
            turn = message is null ? state.GetOpenTurn() : state.BeginTurn(message);
        }
        catch (OperationCanceledException)
        {
            activationCancelled = true;
        }
        catch (Exception ex)
        {
            activationFailure = ex.GetMessageRecursively();
        }

        if (activationCancelled)
        {
            yield break;
        }

        if (activationFailure is not null)
        {
            yield return Res.Fail(activationFailure);
            yield break;
        }

        if (turn is null)
        {
            yield return Res.Fail("Failed to initialize the chat turn.");
            yield break;
        }

        var historyCountBeforeSend = turn.HistoryCheckpoint;
        Exception? streamFailure = null;
        var wasCancelled = false;
        var awaitingApproval = false;
        var turnFinalized = false;
        var updates = approvalResponse is null
            ? _chatService.SendMessageStreamingAsync(state, message!, ct)
            : _chatService.ContinueApprovalStreamingAsync(state, approvalResponse, ct);
        var enumerator = updates.GetAsyncEnumerator(ct);

        try
        {
            while (true)
            {
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    wasCancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    streamFailure = ex;
                }

                if (streamFailure is not null)
                {
                    accumulator.FailRunningToolCalls(streamFailure);
                    CommitAssistantMessageIfAny(accumulator, state, turn, historyCountBeforeSend);
                    state.RecordError(turn, streamFailure.GetMessageRecursively());
                    turnFinalized = true;
                    yield return Res.Fail(streamFailure.GetMessageRecursively());
                    break;
                }

                var update = enumerator.Current;
                foreach (var content in update.Contents)
                {
                    accumulator.ProcessContent(content, update);
                    foreach (var streamEvent in ConvertContent(state, accumulator, content))
                    {
                        awaitingApproval |= streamEvent is ChatApprovalRequestEvent;
                        yield return Res.Ok<ChatStreamEvent>(streamEvent);
                    }
                }
            }
        }
        finally
        {
            if (!turnFinalized)
            {
                CommitAssistantMessageIfAny(accumulator, state, turn, historyCountBeforeSend);
            }
            await enumerator.DisposeAsync();
        }

        if (streamFailure is null)
        {
            yield return Res.Ok<ChatStreamEvent>(new ChatCompletedEvent(wasCancelled, awaitingApproval));
        }
    }

    /// <summary>
    /// Edit a user message and resend (discards all messages after it).
    /// </summary>
    public IAsyncEnumerable<Res<ChatStreamEvent>> EditMessageAsync(
        ChatSession state,
        string messageId,
        string newContent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return EditActivatedSessionAsync(state, messageId, newContent, ct);
    }

    /// <summary>
    /// Retry an AI message (regenerate response for the previous user message).
    /// </summary>
    public IAsyncEnumerable<Res<ChatStreamEvent>> RetryMessageAsync(
        ChatSession state,
        string messageId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return RetryActivatedSessionAsync(state, messageId, ct);
    }

    /// <summary>
    /// Records a UI-side generation failure against the latest turn when stream consumption itself fails.
    /// </summary>
    public Res RecordError(ChatSession state, string error)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            state.RecordError(error);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    /// <summary>
    /// Continues the open turn after the user approves or rejects an external script invocation.
    /// Approval objects stay private to the session and are addressed by an opaque identifier.
    /// </summary>
    public IAsyncEnumerable<Res<ChatStreamEvent>> ContinueApprovalAsync(
        ChatSession state,
        string approvalId,
        bool approved,
        string? reason = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(state);
            var request = state.TakePendingApproval(approvalId);
            return ProcessStreamAsync(state, message: null, request.CreateResponse(approved, reason), ct);
        }
        catch (Exception ex)
        {
            return FailedStream(ex.GetMessageRecursively());
        }
    }

    private async IAsyncEnumerable<Res<ChatStreamEvent>> EditActivatedSessionAsync(
        ChatSession state,
        string messageId,
        string newContent,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? content = null;
        string? failure = null;
        var cancelled = false;
        try
        {
            await _chatService.ActivateSessionAsync(state, ct);
            content = state.RewindForEdit(messageId, newContent);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            failure = ex.GetMessageRecursively();
        }

        if (cancelled)
        {
            yield break;
        }

        if (failure is not null)
        {
            yield return Res.Fail(failure);
            yield break;
        }

        await foreach (var update in SendMessageStreamingAsync(state, content!, ct))
        {
            yield return update;
        }
    }

    private async IAsyncEnumerable<Res<ChatStreamEvent>> RetryActivatedSessionAsync(
        ChatSession state,
        string messageId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? content = null;
        string? failure = null;
        var cancelled = false;
        try
        {
            await _chatService.ActivateSessionAsync(state, ct);
            content = state.RewindForRetry(messageId);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            failure = ex.GetMessageRecursively();
        }

        if (cancelled)
        {
            yield break;
        }

        if (failure is not null)
        {
            yield return Res.Fail(failure);
            yield break;
        }

        await foreach (var update in SendMessageStreamingAsync(state, content!, ct))
        {
            yield return update;
        }
    }

    private static async IAsyncEnumerable<Res<ChatStreamEvent>> FailedStream(string message)
    {
        await Task.CompletedTask;
        yield return Res.Fail(message);
    }

    private static IEnumerable<ChatStreamEvent> ConvertContent(
        ChatSession state,
        StreamingContentAccumulator accumulator,
        AIContent content)
    {
        switch (content)
        {
            case TextReasoningContent reasoning when !string.IsNullOrEmpty(reasoning.Text):
                yield return new ChatReasoningDeltaEvent(reasoning.Text);
                break;
            case TextContent text when !string.IsNullOrEmpty(text.Text):
                yield return new ChatTextDeltaEvent(text.Text);
                break;
            case FunctionCallContent functionCall:
                yield return new ChatToolEvent(
                    functionCall.Name,
                    functionCall.CallId ?? string.Empty,
                    ChatToolEventStatus.Started,
                    ToolCallContentSerializer.SerializeArguments(functionCall.Arguments),
                    Result: null,
                    Error: null);
                break;
            case FunctionResultContent functionResult:
            {
                var call = accumulator.ToolCalls.LastOrDefault(item => item.CallId == functionResult.CallId);
                yield return new ChatToolEvent(
                    call?.ToolName ?? "Unknown Tool",
                    functionResult.CallId ?? string.Empty,
                    call?.Status == ToolCallStatus.Failed
                        ? ChatToolEventStatus.Failed
                        : ChatToolEventStatus.Completed,
                    call?.ArgumentsText,
                    call?.ResultText,
                    call?.ExceptionMessage);
                break;
            }
            case ToolApprovalRequestContent approval when approval.ToolCall is FunctionCallContent functionCall:
            {
                var approvalId = state.StorePendingApproval(approval);
                var arguments = ToolCallContentSerializer.SerializeArguments(functionCall.Arguments);
                yield return new ChatApprovalRequestEvent(
                    approvalId,
                    GetStringArgument(functionCall.Arguments, "skillName", "skill_name") ?? "Unknown skill",
                    GetStringArgument(functionCall.Arguments, "scriptName", "script_name") ?? functionCall.Name,
                    "External file or subprocess script",
                    arguments ?? "{}");
                break;
            }
        }
    }

    private static string? GetStringArgument(
        IDictionary<string, object?>? arguments,
        params string[] names)
    {
        if (arguments is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (arguments.TryGetValue(name, out var value))
            {
                return value?.ToString();
            }
        }

        return null;
    }

    private static List<ToolCallInfo>? MergeToolCalls(
        List<ToolCallInfo>? streamedToolCalls,
        List<ToolCallInfo> historyToolCalls)
    {
        if (historyToolCalls.Count == 0)
        {
            return streamedToolCalls;
        }

        if (streamedToolCalls is not { Count: > 0 })
        {
            return historyToolCalls;
        }

        var merged = new List<ToolCallInfo>(streamedToolCalls);
        foreach (var historyCall in historyToolCalls)
        {
            var existingIndex = merged.FindIndex(call =>
                !string.IsNullOrWhiteSpace(call.CallId)
                && call.CallId == historyCall.CallId);

            if (existingIndex < 0)
            {
                merged.Add(historyCall);
                continue;
            }

            var existingCall = merged[existingIndex];
            merged[existingIndex] = existingCall with
            {
                Arguments = existingCall.Arguments ?? historyCall.Arguments,
                ArgumentsText = existingCall.ArgumentsText ?? historyCall.ArgumentsText,
                ResultText = string.IsNullOrWhiteSpace(existingCall.ResultText)
                    ? historyCall.ResultText
                    : existingCall.ResultText,
                ExceptionMessage = string.IsNullOrWhiteSpace(existingCall.ExceptionMessage)
                    ? historyCall.ExceptionMessage
                    : existingCall.ExceptionMessage,
                Status = existingCall.Status == ToolCallStatus.Running
                    ? historyCall.Status
                    : existingCall.Status,
                CompletedAt = existingCall.CompletedAt ?? historyCall.CompletedAt
            };
        }

        return merged;
    }

    private static void CommitAssistantMessageIfAny(
        StreamingContentAccumulator accumulator,
        ChatSession state,
        ChatTurn turn,
        int historyCountBeforeSend)
    {
        var toolCallsFromHistory = ToolCallHistoryExtractor.Extract(state.ChatHistory, historyCountBeforeSend);
        if (!accumulator.HasBufferedContent && toolCallsFromHistory.Count == 0)
        {
            return;
        }

        var aiMessage = accumulator.CreateMessage(state.ProviderId ?? string.Empty, state.ModelName ?? string.Empty);
        if (toolCallsFromHistory.Count > 0)
        {
            aiMessage.ToolCalls = MergeToolCalls(aiMessage.ToolCalls, toolCallsFromHistory);
        }

        state.CompleteTurn(turn, aiMessage);
    }
}
