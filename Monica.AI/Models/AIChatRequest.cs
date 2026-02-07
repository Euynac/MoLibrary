namespace Monica.AI.Models;

/// <summary>
/// AI 聊天请求模型
/// </summary>
public class AIChatRequest
{
    /// <summary>
    /// 会话 ID（可选，不提供则创建新会话）
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 用户消息
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Provider ID（可选，使用默认 Provider）
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// 模型名称（可选，使用 Provider 默认模型）
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// 系统提示词（可选）
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 是否使用流式响应
    /// </summary>
    public bool Streaming { get; set; } = true;

    /// <summary>
    /// Whether to enable reasoning/thinking mode for this request
    /// </summary>
    public bool ReasoningEnabled { get; set; }
}

/// <summary>
/// AI 聊天响应模型
/// </summary>
public class AIChatResponse
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public required AIChatMessage Message { get; init; }

    /// <summary>
    /// 使用的 Provider ID
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    public string? ModelName { get; init; }
}

/// <summary>
/// 会话创建请求
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// 会话标题（可选）
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Provider ID（可选）
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// 模型名称（可选）
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// 系统提示词（可选）
    /// </summary>
    public string? SystemPrompt { get; set; }
}
