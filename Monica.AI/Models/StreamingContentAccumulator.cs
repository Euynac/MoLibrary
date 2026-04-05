using System.Diagnostics;
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

    /// <summary>
    /// Process a single AIContent item from the stream
    /// </summary>
    public void ProcessContent(AIContent content)
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
            ToolCalls = ToolCalls.Count > 0 ? ToolCalls : null
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
        var exceptionMessage = functionResult.Exception?.ToString();
        var matchingIndex = FindMatchingFunctionResultIndex(functionResult);
        if (matchingIndex >= 0)
        {
            ToolCalls[matchingIndex] = ToolCalls[matchingIndex] with
            {
                ResultText = ToolCallContentSerializer.SerializeResult(functionResult.Result),
                ExceptionMessage = exceptionMessage,
                Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage),
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
            Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage),
            StartedAt = completedAt,
            CompletedAt = completedAt
        });
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
