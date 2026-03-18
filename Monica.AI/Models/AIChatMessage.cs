using Microsoft.Extensions.AI;

namespace Monica.AI.Models;

/// <summary>
/// AI 聊天消息模型
/// </summary>
public class AIChatMessage
{
    /// <summary>
    /// 消息唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 消息角色
    /// </summary>
    public required AIChatRole Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 使用的模型名称（仅助手消息有效）
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// 使用的 Provider ID（仅助手消息有效）
    /// </summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Token 使用量
    /// </summary>
    public TokenUsage? Usage { get; set; }

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
    /// 将消息转换为 Microsoft.Extensions.AI 的 ChatMessage
    /// </summary>
    public ChatMessage ToChatMessage()
    {
        return new ChatMessage(Role.ToChatRole(), Content);
    }

    /// <summary>
    /// 从 Microsoft.Extensions.AI 的 ChatMessage 创建
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
/// 聊天消息角色
/// </summary>
public enum AIChatRole
{
    /// <summary>
    /// 系统消息
    /// </summary>
    System,

    /// <summary>
    /// 用户消息
    /// </summary>
    User,

    /// <summary>
    /// 助手消息
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具消息
    /// </summary>
    Tool
}

/// <summary>
/// 聊天角色扩展方法
/// </summary>
public static class AIChatRoleExtensions
{
    /// <summary>
    /// 转换为 Microsoft.Extensions.AI 的 ChatRole
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
    /// 从 Microsoft.Extensions.AI 的 ChatRole 转换
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
/// Token 使用量
/// </summary>
public class TokenUsage
{
    /// <summary>
    /// 输入 Token 数量
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// 输出 Token 数量
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Reasoning Token 数量
    /// </summary>
    public int ReasoningTokens { get; init; }

    /// <summary>
    /// Total token count
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens + ReasoningTokens;
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
