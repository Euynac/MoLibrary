using Microsoft.Extensions.AI;

namespace MoLibrary.AI.Models;

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
    /// 是否正在流式传输中
    /// </summary>
    public bool IsStreaming { get; set; }

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
    /// 总 Token 数量
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
