using Microsoft.Extensions.AI;
using Monica.AI.Models;

namespace Monica.AI.Extensions;

/// <summary>
/// Extracts tool-call details from chat history when streaming updates do not expose them directly.
/// </summary>
public static class ToolCallHistoryExtractor
{
    public static List<ToolCallInfo> Extract(IList<ChatMessage>? chatHistory, int startIndex)
    {
        if (chatHistory is null || chatHistory.Count == 0)
        {
            return [];
        }

        startIndex = Math.Clamp(startIndex, 0, chatHistory.Count);
        var extractedAt = DateTimeOffset.UtcNow;
        var toolCalls = new List<ToolCallInfo>();
        var toolCallLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var message in chatHistory.Skip(startIndex))
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent functionCall)
                {
                    var callId = functionCall.CallId ?? string.Empty;
                    toolCallLookup[callId] = toolCalls.Count;
                    toolCalls.Add(new ToolCallInfo
                    {
                        ToolName = functionCall.Name,
                        CallId = callId,
                        Arguments = functionCall.Arguments,
                        ArgumentsText = ToolCallContentSerializer.SerializeArguments(functionCall.Arguments),
                        Status = ToolCallStatus.Running,
                        StartedAt = extractedAt
                    });
                }
                else if (content is FunctionResultContent functionResult)
                {
                    AttachFunctionResult(toolCalls, toolCallLookup, functionResult, extractedAt);
                }
            }

            if (message.Role == ChatRole.Tool && !string.IsNullOrWhiteSpace(message.Text))
            {
                AttachToolTextResult(toolCalls, message.Text, extractedAt);
            }
        }

        FinalizePendingCalls(toolCalls, extractedAt);
        return toolCalls;
    }

    private static void AttachFunctionResult(
        List<ToolCallInfo> toolCalls,
        IReadOnlyDictionary<string, int> toolCallLookup,
        FunctionResultContent functionResult,
        DateTimeOffset completedAt)
    {
        var callId = functionResult.CallId ?? string.Empty;
        var exceptionMessage = functionResult.Exception?.ToString();

        if (toolCallLookup.TryGetValue(callId, out var index))
        {
            toolCalls[index] = toolCalls[index] with
            {
                ResultText = ToolCallContentSerializer.SerializeResult(functionResult.Result),
                ExceptionMessage = exceptionMessage,
                Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage),
                CompletedAt = completedAt
            };
            return;
        }

        toolCalls.Add(new ToolCallInfo
        {
            ToolName = "Unknown Tool",
            CallId = callId,
            ResultText = ToolCallContentSerializer.SerializeResult(functionResult.Result),
            ExceptionMessage = exceptionMessage,
            Status = ToolCallContentSerializer.GetFinalStatus(exceptionMessage),
            StartedAt = completedAt,
            CompletedAt = completedAt
        });
    }

    private static void AttachToolTextResult(
        List<ToolCallInfo> toolCalls,
        string toolText,
        DateTimeOffset completedAt)
    {
        var runningIndex = toolCalls.FindLastIndex(call => call.Status == ToolCallStatus.Running);
        if (runningIndex >= 0)
        {
            toolCalls[runningIndex] = toolCalls[runningIndex] with
            {
                ResultText = ToolCallContentSerializer.SerializeResult(toolText),
                Status = ToolCallStatus.Completed,
                CompletedAt = completedAt
            };
            return;
        }

        toolCalls.Add(new ToolCallInfo
        {
            ToolName = "Unknown Tool",
            CallId = string.Empty,
            ResultText = ToolCallContentSerializer.SerializeResult(toolText),
            Status = ToolCallStatus.Completed,
            StartedAt = completedAt,
            CompletedAt = completedAt
        });
    }

    private static void FinalizePendingCalls(List<ToolCallInfo> toolCalls, DateTimeOffset completedAt)
    {
        for (var index = 0; index < toolCalls.Count; index++)
        {
            if (toolCalls[index].Status != ToolCallStatus.Running)
            {
                continue;
            }

            toolCalls[index] = toolCalls[index] with
            {
                Status = ToolCallStatus.Completed,
                CompletedAt = completedAt
            };
        }
    }
}
