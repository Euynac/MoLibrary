using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.AI.Services;
using MoLibrary.AI.UI.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.UI.Services;

/// <summary>
/// AI 聊天 UI 服务
/// </summary>
public class AIChatUIService(
    AIChatService chatService,
    IAIProviderFactory providerFactory,
    ChatSessionStorage sessionStorage,
    IOptions<ModuleAIUIOption> options)
{
    private readonly ModuleAIUIOption _options = options.Value;

    /// <summary>
    /// 获取会话存储
    /// </summary>
    public ChatSessionStorage SessionStorage => sessionStorage;

    /// <summary>
    /// 获取所有 Provider 信息
    /// </summary>
    public IReadOnlyList<AIProviderInfo> GetProviders()
    {
        return providerFactory.GetAllProviderInfos();
    }

    /// <summary>
    /// 获取默认 Provider
    /// </summary>
    public AIProviderInfo? GetDefaultProvider()
    {
        return providerFactory.GetDefaultProvider()?.Info;
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    public ChatSessionInfo CreateSession(string? providerId = null, string? title = null)
    {
        var session = chatService.CreateSession(
            providerId,
            title,
            _options.DefaultSystemPrompt);

        var sessionInfo = new ChatSessionInfo
        {
            SessionId = session.SessionId,
            Title = session.Title,
            ProviderId = session.ProviderId,
            ModelName = session.ModelName
        };

        sessionStorage.AddSession(sessionInfo);
        sessionStorage.CurrentSessionId = session.SessionId;

        return sessionInfo;
    }

    /// <summary>
    /// 获取或创建会话
    /// </summary>
    public ChatSessionInfo GetOrCreateSession(string? sessionId = null, string? providerId = null)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            var existing = sessionStorage.GetSession(sessionId);
            if (existing != null)
            {
                return existing;
            }
        }

        return CreateSession(providerId);
    }

    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    public async Task<Res<AIChatMessage>> SendMessageAsync(
        string sessionId,
        string message,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = false,
            ProviderId = sessionInfo?.ProviderId
        };

        var result = await chatService.SendMessageAsync(request, ct);
        if (result.IsFailed(out var error, out var response))
        {
            return error;
        }

        // 更新本地会话存储
        if (sessionInfo != null)
        {
            // 添加用户消息到本地存储
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });

            // 添加助手响应到本地存储
            sessionInfo.Messages.Add(response.Message);
            sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;

            // 更新标题
            if (sessionInfo.Messages.Count == 2)
            {
                sessionInfo.Title = message.Length > 50 ? message[..50] + "..." : message;
            }
        }

        return response.Message;
    }

    /// <summary>
    /// 发送消息并获取流式响应
    /// </summary>
    /// <remarks>
    /// User message is added synchronously before returning the async enumerable,
    /// ensuring it appears in the UI immediately when the caller updates state.
    /// </remarks>
    public IAsyncEnumerable<ChatResponseUpdate> SendMessageStreamingAsync(
        string sessionId,
        string message,
        CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo != null)
        {
            // 添加用户消息到本地存储（同步执行，确保调用者可以立即看到）
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.User,
                Content = message
            });

            // 更新标题（第一条消息）
            if (sessionInfo.Messages.Count == 1)
            {
                sessionInfo.Title = message.Length > 50 ? message[..50] + "..." : message;
            }
        }

        // 返回异步流式响应
        return StreamResponseAsync(sessionId, message, sessionInfo, ct);
    }

    /// <summary>
    /// 内部方法：处理流式响应
    /// </summary>
    private async IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
        string sessionId,
        string message,
        ChatSessionInfo? sessionInfo,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new AIChatRequest
        {
            SessionId = sessionId,
            Message = message,
            Streaming = true,
            ProviderId = sessionInfo?.ProviderId
        };

        var fullContent = string.Empty;

        await foreach (var update in chatService.SendMessageStreamingAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent += update.Text;
            }

            yield return update;
        }

        // 流式响应完成后，添加完整的助手消息到本地存储
        if (sessionInfo != null)
        {
            sessionInfo.Messages.Add(new AIChatMessage
            {
                Role = AIChatRole.Assistant,
                Content = fullContent,
                ProviderId = sessionInfo.ProviderId,
                ModelName = sessionInfo.ModelName
            });
            sessionInfo.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Edit a user message and resend (discards all messages after it)
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> EditMessageAsync(
        string sessionId,
        string messageId,
        string newContent,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo == null)
        {
            yield break;
        }

        // Find message index
        var index = sessionInfo.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            yield break;
        }

        // Remove all messages from this index onwards
        sessionInfo.Messages.RemoveRange(index, sessionInfo.Messages.Count - index);

        // Re-send the edited message
        await foreach (var update in SendMessageStreamingAsync(sessionId, newContent, ct))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Retry an AI message (regenerate response for the previous user message)
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> RetryMessageAsync(
        string sessionId,
        string messageId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionInfo = sessionStorage.GetSession(sessionId);
        if (sessionInfo == null)
        {
            yield break;
        }

        // Find the AI message index
        var index = sessionInfo.Messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            yield break;
        }

        // Find the previous user message
        var userMessage = sessionInfo.Messages.Take(index).LastOrDefault(m => m.Role == AIChatRole.User);
        if (userMessage == null)
        {
            yield break;
        }

        // Remove the AI message (and any after it)
        sessionInfo.Messages.RemoveRange(index, sessionInfo.Messages.Count - index);

        // Re-send the user message to get new AI response
        await foreach (var update in SendMessageStreamingAsync(sessionId, userMessage.Content, ct))
        {
            yield return update;
        }
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        var deleted = chatService.DeleteSession(sessionId);
        if (deleted)
        {
            sessionStorage.RemoveSession(sessionId);
        }
        return deleted;
    }

    /// <summary>
    /// 切换会话
    /// </summary>
    public void SwitchSession(string sessionId)
    {
        sessionStorage.CurrentSessionId = sessionId;
    }

    /// <summary>
    /// 加载会话列表
    /// </summary>
    public void LoadSessions()
    {
        var sessions = chatService.GetAllSessions();
        var sessionInfos = sessions.Select(s => new ChatSessionInfo
        {
            SessionId = s.SessionId,
            Title = s.Title,
            ProviderId = s.ProviderId,
            ModelName = s.ModelName,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        // 加载每个会话的消息
        foreach (var sessionInfo in sessionInfos)
        {
            var session = chatService.GetSession(sessionInfo.SessionId);
            if (session != null)
            {
                sessionInfo.Messages.AddRange(session.Messages);
            }
        }

        sessionStorage.LoadSessions(sessionInfos);
    }
}
