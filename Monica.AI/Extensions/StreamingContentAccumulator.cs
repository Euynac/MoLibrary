using System.Diagnostics;
using Microsoft.Extensions.AI;
using Monica.AI.Models;

namespace Monica.AI.Extensions;

/// <summary>
/// Utility for accumulating streaming content from AgentResponseUpdate.
/// Processes text, reasoning, and tool calls into a final AIChatMessage.
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
            ToolCalls.Add(new ToolCallInfo(
                functionCall.Name,
                functionCall.CallId ?? string.Empty,
                functionCall.Arguments,
                null,
                DateTimeOffset.UtcNow));
        }
        else if (content is FunctionResultContent functionResult)
        {
            var matching = ToolCalls.FindIndex(t => t.CallId == functionResult.CallId);
            if (matching >= 0)
            {
                ToolCalls[matching] = ToolCalls[matching] with
                {
                    Result = functionResult.Result?.ToString()
                };
            }
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
}
