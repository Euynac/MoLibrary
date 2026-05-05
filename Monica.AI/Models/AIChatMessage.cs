using Microsoft.Extensions.AI;

namespace Monica.AI.Models;

/// <summary>
/// Represents an AI chat message.
/// </summary>
public class AIChatMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Message role.
    /// </summary>
    public required AIChatRole Role { get; init; }

    /// <summary>
    /// Message content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Time when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The model name used for this message. Only applies to assistant messages.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Provider ID used for this message. Only applies to assistant messages.
    /// </summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Token usage
    /// </summary>
    public TokenUsage? Usage { get; set; }

    /// <summary>
    /// Per-service-call token usage records captured while generating this assistant message.
    /// Agent runs with tools can make multiple provider requests before producing the final answer.
    /// </summary>
    public List<AIChatRequestUsage>? RequestUsages { get; set; }

    /// <summary>
    /// Reasoning/thinking content from the model (e.g., OpenAI o1, Claude extended thinking)
    /// </summary>
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// Duration in seconds the model spent reasoning
    /// </summary>
    public double? ReasoningDurationSeconds { get; set; }

    /// <summary>
    /// Whether the message is currently streaming
    /// </summary>
    public bool IsStreaming { get; set; }

    /// <summary>
    /// Tool calls made during this message's generation.
    /// Populated from FunctionCallContent/FunctionResultContent in AgentResponseUpdate.
    /// </summary>
    public List<ToolCallInfo>? ToolCalls { get; set; }

    /// <summary>
    /// Converts this message to a <see cref="ChatMessage"/> from Microsoft.Extensions.AI.
    /// </summary>
    public ChatMessage ToChatMessage()
    {
        return new ChatMessage(Role.ToChatRole(), Content);
    }

    /// <summary>
    /// Creates an <see cref="AIChatMessage"/> from a <see cref="ChatMessage"/>.
    /// </summary>
    public static AIChatMessage FromChatMessage(ChatMessage message, string? providerId = null, string? modelName = null)
    {
        return new AIChatMessage
        {
            Role = AIChatRoleExtensions.FromChatRole(message.Role),
            Content = message.Text ?? string.Empty,
            ProviderId = providerId,
            ModelName = modelName
        };
    }
}

/// <summary>
/// Chat message roles.
/// </summary>
public enum AIChatRole
{
    /// <summary>
    /// System message.
    /// </summary>
    System,

    /// <summary>
    /// User message.
    /// </summary>
    User,

    /// <summary>
    /// Assistant message.
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool message.
    /// </summary>
    Tool
}

/// <summary>
/// Extensions for converting chat roles.
/// </summary>
public static class AIChatRoleExtensions
{
    /// <summary>
    /// Converts an <see cref="AIChatRole"/> to a <see cref="ChatRole"/>.
    /// </summary>
    public static ChatRole ToChatRole(this AIChatRole role)
    {
        return role switch
        {
            AIChatRole.System => ChatRole.System,
            AIChatRole.User => ChatRole.User,
            AIChatRole.Assistant => ChatRole.Assistant,
            AIChatRole.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };
    }

    /// <summary>
    /// Converts a <see cref="ChatRole"/> to an <see cref="AIChatRole"/>.
    /// </summary>
    public static AIChatRole FromChatRole(ChatRole role)
    {
        if (role == ChatRole.System) return AIChatRole.System;
        if (role == ChatRole.User) return AIChatRole.User;
        if (role == ChatRole.Assistant) return AIChatRole.Assistant;
        if (role == ChatRole.Tool) return AIChatRole.Tool;
        return AIChatRole.User;
    }
}

/// <summary>
/// Token usage information.
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// Number of input tokens.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Number of reasoning tokens reported by the provider.
    /// These tokens are normally included in <see cref="OutputTokens"/>.
    /// </summary>
    public int ReasoningTokens { get; init; }

    /// <summary>
    /// Number of input tokens served from the provider prompt cache.
    /// These tokens are included in <see cref="InputTokens"/>.
    /// </summary>
    public int CachedInputTokens { get; init; }

    /// <summary>
    /// Total token count reported by the provider.
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Total token count to display when the provider omitted an explicit total.
    /// </summary>
    public int EffectiveTotalTokens => TotalTokens > 0 ? TotalTokens : InputTokens + OutputTokens;

    /// <summary>
    /// Fraction of input tokens served from the provider prompt cache.
    /// </summary>
    public double CachedInputRatio => InputTokens > 0 ? (double)CachedInputTokens / InputTokens : 0;

    /// <summary>
    /// Sum multiple usage records into one aggregate usage record.
    /// </summary>
    public static TokenUsage Sum(IEnumerable<TokenUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);

        var inputTokens = 0;
        var outputTokens = 0;
        var reasoningTokens = 0;
        var cachedInputTokens = 0;
        var totalTokens = 0;

        foreach (var usage in usages)
        {
            inputTokens += usage.InputTokens;
            outputTokens += usage.OutputTokens;
            reasoningTokens += usage.ReasoningTokens;
            cachedInputTokens += usage.CachedInputTokens;
            totalTokens += usage.EffectiveTotalTokens;
        }

        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            CachedInputTokens = cachedInputTokens,
            TotalTokens = totalTokens
        };
    }
}

/// <summary>
/// Token usage for one underlying provider request made during an agent run.
/// </summary>
public sealed class AIChatRequestUsage
{
    /// <summary>
    /// One-based order of the provider request within the assistant message generation.
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// Time when the usage record was observed.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Provider response identifier, when the agent framework exposed one.
    /// </summary>
    public string? ResponseId { get; init; }

    /// <summary>
    /// Provider message identifier, when the agent framework exposed one.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Token usage reported for this provider request.
    /// </summary>
    public required TokenUsage Usage { get; init; }
}

/// <summary>
/// Information about a tool call made during message generation.
/// </summary>
public sealed record ToolCallInfo
{
    /// <summary>
    /// Tool name reported by the model.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Unique tool call identifier.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// Raw tool arguments.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; init; }

    /// <summary>
    /// Pretty-printed tool arguments for debug display.
    /// </summary>
    public string? ArgumentsText { get; init; }

    /// <summary>
    /// Pretty-printed tool output for debug display.
    /// </summary>
    public string? ResultText { get; init; }

    /// <summary>
    /// Tool execution exception text, when available.
    /// </summary>
    public string? ExceptionMessage { get; init; }

    /// <summary>
    /// Current execution status of the tool call.
    /// </summary>
    public ToolCallStatus Status { get; init; }

    /// <summary>
    /// When the tool call started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the tool call finished.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan? Duration => CompletedAt is { } completedAt ? completedAt - StartedAt : null;
}

/// <summary>
/// Execution status for a tool call.
/// </summary>
public enum ToolCallStatus
{
    /// <summary>
    /// The tool call has been requested and is still running.
    /// </summary>
    Running,

    /// <summary>
    /// The tool call finished successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The tool call finished with an error.
    /// </summary>
    Failed
}
