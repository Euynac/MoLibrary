using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Tools;

/// <summary>
/// Adds per-run chat-option configuration to the agent pipeline.
/// </summary>
public static class AIAgentBuilderRunChatOptionsExtensions
{
    /// <summary>
    /// Ensures the provided chat options are merged into every agent invocation.
    /// </summary>
    public static AIAgentBuilder UseConfiguredRunChatOptions(
        this AIAgentBuilder builder,
        ChatOptions chatOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(chatOptions);

        var configuredChatOptions = chatOptions.Clone();

        return builder.Use(
            runFunc: (messages, session, options, innerAgent, cancellationToken) =>
                innerAgent.RunAsync(
                    messages,
                    session,
                    MergeRunOptions(options, configuredChatOptions),
                    cancellationToken),
            runStreamingFunc: (messages, session, options, innerAgent, cancellationToken) =>
                innerAgent.RunStreamingAsync(
                    messages,
                    session,
                    MergeRunOptions(options, configuredChatOptions),
                    cancellationToken));
    }

    private static ChatClientAgentRunOptions MergeRunOptions(
        AgentRunOptions? options,
        ChatOptions configuredChatOptions)
    {
        var mergedOptions = options?.Clone() switch
        {
            ChatClientAgentRunOptions chatClientRunOptions => chatClientRunOptions,
            AgentRunOptions agentRunOptions => new ChatClientAgentRunOptions
            {
                AllowBackgroundResponses = agentRunOptions.AllowBackgroundResponses,
                AdditionalProperties = agentRunOptions.AdditionalProperties,
                ResponseFormat = agentRunOptions.ResponseFormat
            },
            _ => new ChatClientAgentRunOptions()
        };

        mergedOptions.ChatOptions = MergeChatOptions(mergedOptions.ChatOptions, configuredChatOptions);
        return mergedOptions;
    }

    private static ChatOptions MergeChatOptions(
        ChatOptions? runChatOptions,
        ChatOptions configuredChatOptions)
    {
        var mergedChatOptions = runChatOptions?.Clone() ?? new ChatOptions();
        mergedChatOptions.ToolMode ??= configuredChatOptions.ToolMode;
        mergedChatOptions.AllowMultipleToolCalls ??= configuredChatOptions.AllowMultipleToolCalls;

        if (configuredChatOptions.Tools is not { Count: > 0 })
        {
            return mergedChatOptions;
        }

        if (mergedChatOptions.Tools is not { Count: > 0 })
        {
            mergedChatOptions.Tools = [.. configuredChatOptions.Tools];
            return mergedChatOptions;
        }

        if (mergedChatOptions.Tools is List<AITool> runTools)
        {
            runTools.AddRange(configuredChatOptions.Tools);
            return mergedChatOptions;
        }

        foreach (var tool in configuredChatOptions.Tools)
        {
            mergedChatOptions.Tools.Add(tool);
        }

        return mergedChatOptions;
    }
}
