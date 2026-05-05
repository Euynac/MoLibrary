using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Monica.AI.Services.Support;

namespace Monica.AI.Models;

/// <summary>
/// Accumulates streaming agent content into a final assistant chat message.
/// </summary>
public class StreamingContentAccumulator
{
    /// <summary>
    /// Accumulated text content
    /// </summary>
    public string FullContent { get; private set; } = string.Empty;

    /// <summary>
    /// Accumulated reasoning content
    /// </summary>
    public string FullReasoning { get; private set; } = string.Empty;

    /// <summary>
    /// Stopwatch for tracking reasoning duration
    /// </summary>
    public Stopwatch ReasoningStopwatch { get; } = new();

    /// <summary>
    /// Accumulated tool calls
    /// </summary>
    public List<ToolCallInfo> ToolCalls { get; } = new();

    private readonly Dictionary<string, RequestUsageBuffer> _requestUsageBuffers = new(StringComparer.Ordinal);
    private readonly List<RequestUsageBuffer> _requestUsageOrder = [];

    internal bool HasBufferedContent
        => !string.IsNullOrEmpty(FullContent)
           || !string.IsNullOrEmpty(FullReasoning)
           || ToolCalls.Count > 0
           || _requestUsageOrder.Count > 0;

    /// <summary>
    /// Process a single AIContent item from the stream
    /// </summary>
    public void ProcessContent(AIContent content, AgentResponseUpdate? update = null)
    {
        if (content is TextReasoningContent reasoning && !string.IsNullOrEmpty(reasoning.Text))
        {
            if (!ReasoningStopwatch.IsRunning)
                ReasoningStopwatch.Start();
            FullReasoning += reasoning.Text;
        }
        else if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
        {
            if (ReasoningStopwatch.IsRunning)
                ReasoningStopwatch.Stop();
            FullContent += text.Text;
        }
        else if (content is FunctionCallContent functionCall)
        {
            ProcessFunctionCall(functionCall);
        }
        else if (content is FunctionResultContent functionResult)
        {
            ProcessFunctionResult(functionResult);
        }
        else if (content is UsageContent usage)
        {
            ProcessUsage(usage, update);
        }
    }

    /// <summary>
    /// Create final AIChatMessage from accumulated content
    /// </summary>
    public AIChatMessage CreateMessage(string providerId, string modelName)
    {
        if (ReasoningStopwatch.IsRunning)
            ReasoningStopwatch.Stop();

        return new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Content = FullContent,
            ProviderId = providerId,
            ModelName = modelName,
            ReasoningContent = string.IsNullOrEmpty(FullReasoning) ? null : FullReasoning,
            ReasoningDurationSeconds = ReasoningStopwatch.Elapsed.TotalSeconds > 0
                ? ReasoningStopwatch.Elapsed.TotalSeconds
                : null,
            ToolCalls = ToolCalls.Count > 0 ? ToolCalls : null,
            RequestUsages = _requestUsageOrder.Count > 0 ? BuildRequestUsages() : null,
            Usage = _requestUsageOrder.Count > 0
                ? TokenUsage.Sum(_requestUsageOrder.Select(buffer => CreateTokenUsage(buffer.UsageDetails)))
                : null
        };
    }

    /// <summary>
    /// Reset accumulator state for reuse
    /// </summary>
    public void Reset()
    {
        FullContent = string.Empty;
        FullReasoning = string.Empty;
        ReasoningStopwatch.Reset();
        ToolCalls.Clear();
        _requestUsageBuffers.Clear();
        _requestUsageOrder.Clear();
    }

    internal void FailRunningToolCalls(Exception exception)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var exceptionMessage = exception.ToString();

        for (var index = 0; index < ToolCalls.Count; index++)
        {
            if (ToolCalls[index].Status != ToolCallStatus.Running)
            {
                continue;
            }

            ToolCalls[index] = ToolCalls[index] with
            {
                ExceptionMessage = exceptionMessage,
                Status = ToolCallStatus.Failed,
                CompletedAt = completedAt
            };
        }
    }

    private void ProcessFunctionCall(FunctionCallContent functionCall)
    {
        var argumentsText = ToolCallContentSerializer.SerializeArguments(functionCall.Arguments);
        var matchingIndex = FindMatchingFunctionCallIndex(functionCall, argumentsText);
        if (matchingIndex >= 0)
        {
            var existingCall = ToolCalls[matchingIndex];
            ToolCalls[matchingIndex] = existingCall with
            {
                Arguments = functionCall.Arguments ?? existingCall.Arguments,
                ArgumentsText = string.IsNullOrWhiteSpace(argumentsText)
                    ? existingCall.ArgumentsText
                    : argumentsText
            };
            return;
        }

        ToolCalls.Add(new ToolCallInfo
        {
            ToolName = functionCall.Name,
            CallId = functionCall.CallId ?? string.Empty,
            Arguments = functionCall.Arguments,
            ArgumentsText = argumentsText,
            Status = ToolCallStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        });
    }

    private void ProcessFunctionResult(FunctionResultContent functionResult)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var exceptionMessage = ToolCallContentSerializer.GetExceptionMessage(
            functionResult.Result,
            functionResult.Exception);
        var matchingIndex = FindMatchingFunctionResultIndex(functionResult);
        if (matchingIndex >= 0)
        {
            ToolCalls[matchingIndex] = ToolCalls[matchingIndex] with
            {
                ResultText = ToolCallContentSerializer.SerializeResult(functionResult.Result),
                ExceptionMessage = exceptionMessage,
                Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage, functionResult.Result),
                CompletedAt = completedAt
            };
            return;
        }

        ToolCalls.Add(new ToolCallInfo
        {
            ToolName = "Unknown Tool",
            CallId = functionResult.CallId ?? string.Empty,
            ResultText = ToolCallContentSerializer.SerializeResult(functionResult.Result),
            ExceptionMessage = exceptionMessage,
            Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage, functionResult.Result),
            StartedAt = completedAt,
            CompletedAt = completedAt
        });
    }

    private void ProcessUsage(UsageContent usage, AgentResponseUpdate? update)
    {
        if (usage.Details is null)
        {
            return;
        }

        var key = BuildRequestUsageKey(update);
        if (!_requestUsageBuffers.TryGetValue(key, out var buffer))
        {
            buffer = new RequestUsageBuffer(_requestUsageOrder.Count + 1, update);
            _requestUsageBuffers.Add(key, buffer);
            _requestUsageOrder.Add(buffer);
        }

        buffer.Add(usage.Details, update);
    }

    private List<AIChatRequestUsage> BuildRequestUsages()
    {
        if (_requestUsageOrder.Count == 0)
        {
            return [];
        }

        return _requestUsageOrder
            .Select(buffer => new AIChatRequestUsage
            {
                Sequence = buffer.Sequence,
                CreatedAt = buffer.CreatedAt,
                ResponseId = buffer.ResponseId,
                MessageId = buffer.MessageId,
                Usage = CreateTokenUsage(buffer.UsageDetails)
            })
            .ToList();
    }

    private static TokenUsage CreateTokenUsage(UsageDetails usage)
    {
        var inputTokens = ToTokenCount(usage.InputTokenCount);
        var outputTokens = ToTokenCount(usage.OutputTokenCount);
        var totalTokens = ToTokenCount(usage.TotalTokenCount);
        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = ToTokenCount(usage.ReasoningTokenCount),
            CachedInputTokens = ToTokenCount(usage.CachedInputTokenCount),
            TotalTokens = totalTokens > 0 ? totalTokens : inputTokens + outputTokens
        };
    }

    private static int ToTokenCount(long? value)
    {
        if (value is null or <= 0)
        {
            return 0;
        }

        return value > int.MaxValue ? int.MaxValue : (int)value.Value;
    }

    private static string BuildRequestUsageKey(AgentResponseUpdate? update)
    {
        if (!string.IsNullOrWhiteSpace(update?.MessageId))
        {
            return $"message:{update.MessageId}";
        }

        if (!string.IsNullOrWhiteSpace(update?.ResponseId))
        {
            return $"response:{update.ResponseId}";
        }

        return update is null
            ? "update:null"
            : $"update:{RuntimeHelpers.GetHashCode(update)}";
    }

    private sealed class RequestUsageBuffer(int sequence, AgentResponseUpdate? update)
    {
        public int Sequence { get; } = sequence;

        public DateTimeOffset CreatedAt { get; private set; } = update?.CreatedAt ?? DateTimeOffset.UtcNow;

        public string? ResponseId { get; private set; } = update?.ResponseId;

        public string? MessageId { get; private set; } = update?.MessageId;

        public UsageDetails UsageDetails { get; } = new();

        public void Add(UsageDetails details, AgentResponseUpdate? sourceUpdate)
        {
            UsageDetails.Add(details);
            if (sourceUpdate?.CreatedAt is { } createdAt)
            {
                CreatedAt = createdAt;
            }

            ResponseId ??= sourceUpdate?.ResponseId;
            MessageId ??= sourceUpdate?.MessageId;
        }
    }

    private int FindMatchingFunctionCallIndex(FunctionCallContent functionCall, string? argumentsText)
    {
        var callId = functionCall.CallId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(callId))
        {
            return ToolCalls.FindLastIndex(call => string.Equals(call.CallId, callId, StringComparison.Ordinal));
        }

        return ToolCalls.FindLastIndex(call =>
            call.Status == ToolCallStatus.Running
            && string.IsNullOrWhiteSpace(call.ResultText)
            && string.Equals(call.ToolName, functionCall.Name, StringComparison.Ordinal)
            && string.Equals(call.ArgumentsText, argumentsText, StringComparison.Ordinal));
    }

    private int FindMatchingFunctionResultIndex(FunctionResultContent functionResult)
    {
        var callId = functionResult.CallId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(callId))
        {
            var runningMatch = ToolCalls.FindLastIndex(call =>
                string.Equals(call.CallId, callId, StringComparison.Ordinal)
                && call.Status == ToolCallStatus.Running);
            if (runningMatch >= 0)
            {
                return runningMatch;
            }

            return ToolCalls.FindLastIndex(call => string.Equals(call.CallId, callId, StringComparison.Ordinal));
        }

        return ToolCalls.FindLastIndex(call => call.Status == ToolCallStatus.Running);
    }
}
