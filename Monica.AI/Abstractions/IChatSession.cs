using Microsoft.Extensions.AI;
using Monica.AI.Models;
using Monica.Tool.MoResponse;

namespace Monica.AI.Abstractions;

/// <summary>
/// 聊天会话接口，管理单个对话的上下文和消息历史
/// </summary>
public interface IChatSession
{
    /// <summary>
    /// 会话唯一标识符
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// 会话标题
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// 会话创建时间
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 会话最后更新时间
    /// </summary>
    DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// 当前使用的 Provider ID
    /// </summary>
    string ProviderId { get; set; }

    /// <summary>
    /// 当前使用的模型名称
    /// </summary>
    string? ModelName { get; set; }

    /// <summary>
    /// 系统提示词
    /// </summary>
    string? SystemPrompt { get; set; }

    /// <summary>
    /// 消息历史列表
    /// </summary>
    IReadOnlyList<AIChatMessage> Messages { get; }

    /// <summary>
    /// 添加用户消息
    /// </summary>
    /// <param name="content">消息内容</param>
    /// <returns>添加的消息</returns>
    AIChatMessage AddUserMessage(string content);

    /// <summary>
    /// 添加助手消息
    /// </summary>
    /// <param name="content">消息内容</param>
    /// <returns>添加的消息</returns>
    AIChatMessage AddAssistantMessage(string content);

    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>助手响应</returns>
    Task<Res<AIChatMessage>> SendMessageAsync(string message, CancellationToken ct = default);

    /// <summary>
    /// 发送消息并获取流式响应
    /// </summary>
    /// <param name="message">用户消息</param>
    /// <param name="options">Chat options (e.g. for reasoning)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应</returns>
    IAsyncEnumerable<ChatResponseUpdate> SendMessageStreamingAsync(string message, ChatOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// 清空会话历史
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Truncate history to keep only the first N messages
    /// </summary>
    /// <param name="keepCount">Number of messages to keep from the beginning</param>
    void TruncateHistory(int keepCount);

    /// <summary>
    /// 将会话转换为 ChatMessage 列表（用于 IChatClient）
    /// </summary>
    /// <returns>ChatMessage 列表</returns>
    IList<ChatMessage> ToChatMessages();
}
