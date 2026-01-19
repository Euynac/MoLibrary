using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.AI.Modules;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.Services;

/// <summary>
/// AI 聊天服务
/// </summary>
public class AIChatService(IAIProviderFactory providerFactory, IOptions<ModuleAIOption> options)
{
    private readonly ConcurrentDictionary<string, IChatSession> _sessions = new();
    private readonly ModuleAIOption _options = options.Value;

    /// <summary>
    /// 创建新的聊天会话
    /// </summary>
    /// <param name="providerId">Provider ID（可选）</param>
    /// <param name="title">会话标题（可选）</param>
    /// <param name="systemPrompt">系统提示词（可选）</param>
    /// <returns>聊天会话</returns>
    public IChatSession CreateSession(string? providerId = null, string? title = null, string? systemPrompt = null)
    {
        var session = new ChatSession(providerFactory, providerId)
        {
            Title = title ?? "新对话",
            SystemPrompt = systemPrompt ?? _options.DefaultSystemPrompt
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    /// <summary>
    /// 获取会话
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <returns>聊天会话</returns>
    public IChatSession? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    /// <summary>
    /// 获取或创建会话
    /// </summary>
    /// <param name="sessionId">会话 ID（可选）</param>
    /// <param name="providerId">Provider ID（可选）</param>
    /// <returns>聊天会话</returns>
    public IChatSession GetOrCreateSession(string? sessionId = null, string? providerId = null)
    {
        if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        return CreateSession(providerId);
    }

    /// <summary>
    /// 获取所有会话
    /// </summary>
    /// <returns>所有会话列表</returns>
    public IReadOnlyList<IChatSession> GetAllSessions()
    {
        return _sessions.Values.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <returns>是否删除成功</returns>
    public bool DeleteSession(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>聊天响应</returns>
    public async Task<Res<AIChatResponse>> SendMessageAsync(AIChatRequest request, CancellationToken ct = default)
    {
        var session = GetOrCreateSession(request.SessionId, request.ProviderId);

        if (!string.IsNullOrEmpty(request.ProviderId))
        {
            session.ProviderId = request.ProviderId;
        }

        if (!string.IsNullOrEmpty(request.ModelName))
        {
            session.ModelName = request.ModelName;
        }

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            session.SystemPrompt = request.SystemPrompt;
        }

        var result = await session.SendMessageAsync(request.Message, ct);
        if (result.IsFailed(out var error, out var message))
        {
            return error;
        }

        return new AIChatResponse
        {
            SessionId = session.SessionId,
            Message = message,
            ProviderId = session.ProviderId,
            ModelName = session.ModelName
        };
    }

    /// <summary>
    /// 发送消息并获取流式响应
    /// </summary>
    /// <param name="request">聊天请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应</returns>
    public async IAsyncEnumerable<ChatResponseUpdate> SendMessageStreamingAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = GetOrCreateSession(request.SessionId, request.ProviderId);

        if (!string.IsNullOrEmpty(request.ProviderId))
        {
            session.ProviderId = request.ProviderId;
        }

        if (!string.IsNullOrEmpty(request.ModelName))
        {
            session.ModelName = request.ModelName;
        }

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            session.SystemPrompt = request.SystemPrompt;
        }

        await foreach (var update in session.SendMessageStreamingAsync(request.Message, ct))
        {
            yield return update;
        }
    }

    /// <summary>
    /// 获取会话 ID（用于新会话返回）
    /// </summary>
    public string GetSessionIdForRequest(AIChatRequest request)
    {
        var session = GetOrCreateSession(request.SessionId, request.ProviderId);
        return session.SessionId;
    }
}
