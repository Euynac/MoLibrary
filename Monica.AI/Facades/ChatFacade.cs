using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Monica.AI.Abstractions;
using Monica.AI.Models;
using Monica.AI.Services;
using Monica.AI.Services.Support;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.AI.Facades;

/// <summary>
/// Host-facing facade for chat session creation and streaming operations.
/// </summary>
public class ChatFacade(
    AIChatService chatService,
    IAIProviderFactory providerFactory)
{
    /// <summary>
    /// Get all provider info
    /// </summary>
    public IReadOnlyList<AIProviderInfo> GetProviders()
    {
        return providerFactory.GetAllProviderInfos();
    }

    /// <summary>
    /// Get default provider
    /// </summary>
    public AIProviderInfo? GetDefaultProvider()
    {
        return providerFactory.GetDefaultProvider()?.Info;
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
            var state = await chatService.CreateSessionAsync(
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
    /// Sends a message through one chat session and returns streaming agent updates.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> SendMessageStreamingAsync(
        ChatSession state,
        string message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ProcessStreamAsync(message, state, ct);
    }

    /// <summary>
    /// Unified stream processing method that accumulates content, reasoning, and tool calls.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> ProcessStreamAsync(
        string message,
        ChatSession state,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var accumulator = new StreamingContentAccumulator();
        var historyCountBeforeSend = state.ChatHistory?.Count ?? 0;
        var streamFailure = default(Exception);
        var enumerator = chatService.SendMessageStreamingAsync(state, message, ct).GetAsyncEnumerator(ct);

        try
        {
            while (true)
            {
                AgentResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    streamFailure = ex;
                    throw;
                }

                foreach (var content in update.Contents)
                {
                    accumulator.ProcessContent(content, update);
                }

                yield return update;
            }
        }
        finally
        {
            if (streamFailure is not null)
            {
                accumulator.FailRunningToolCalls(streamFailure);
            }

            CommitAssistantMessageIfAny(accumulator, state, historyCountBeforeSend);
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>
    /// Edit a user message and resend (discards all messages after it).
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> EditMessageAsync(
        ChatSession state,
        string messageId,
        string newContent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var index = state.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Remove all messages from this index onwards
        state.Messages.RemoveRange(index, state.Messages.Count - index);

        // Sync backend: truncate agent history to match UI state
        state.TruncateHistory(index);

        return SendMessageStreamingAsync(state, newContent, ct);
    }

    /// <summary>
    /// Retry an AI message (regenerate response for the previous user message).
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate> RetryMessageAsync(
        ChatSession state,
        string messageId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var index = state.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        var userMessage = state.Messages.Take(index).LastOrDefault(m => m.Role == AIChatRole.User);
        if (userMessage == null)
        {
            return AsyncEnumerableEmpty<AgentResponseUpdate>();
        }

        // Find the user message index
        var userMessageIndex = state.Messages.FindIndex(m => m.Id == userMessage.Id);

        // Remove the user message and everything after it (including the AI message to retry)
        state.Messages.RemoveRange(userMessageIndex, state.Messages.Count - userMessageIndex);

        // Sync backend: truncate to remove both user and assistant messages
        state.TruncateHistory(userMessageIndex);

        // Now send the message normally - it will add the user message and new response
        return SendMessageStreamingAsync(state, userMessage.Content, ct);
    }

    /// <summary>
    /// Helper to return an empty async enumerable
    /// </summary>
    private static async IAsyncEnumerable<T> AsyncEnumerableEmpty<T>()
    {
        await Task.CompletedTask;
        yield break;
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

        state.Messages.Add(aiMessage);
        state.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
