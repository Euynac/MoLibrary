using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.Services;

/// <summary>
/// 聊天会话实现
/// </summary>
public class ChatSession : IChatSession
{
    private readonly List<AIChatMessage> _messages = [];
    private readonly IAIProviderFactory _providerFactory;

    public ChatSession(IAIProviderFactory providerFactory, string? providerId = null)
    {
        _providerFactory = providerFactory;
        SessionId = Guid.NewGuid().ToString("N");
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        ProviderId = providerId ?? _providerFactory.GetDefaultProvider()?.ProviderId ?? string.Empty;
    }

    /// <inheritdoc />
    public string SessionId { get; }

    /// <inheritdoc />
    public string Title { get; set; } = "新对话";

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <inheritdoc />
    public string ProviderId { get; set; }

    /// <inheritdoc />
    public string? ModelName { get; set; }

    /// <inheritdoc />
    public string? SystemPrompt { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<AIChatMessage> Messages => _messages.AsReadOnly();

    /// <inheritdoc />
    public AIChatMessage AddUserMessage(string content)
    {
        var message = new AIChatMessage
        {
            Role = AIChatRole.User,
            Content = content
        };
        _messages.Add(message);
        UpdatedAt = DateTimeOffset.UtcNow;

        // 如果是第一条用户消息，自动设置标题
        if (_messages.Count(m => m.Role == AIChatRole.User) == 1)
        {
            Title = content.Length > 50 ? content[..50] + "..." : content;
        }

        return message;
    }

    /// <inheritdoc />
    public AIChatMessage AddAssistantMessage(string content)
    {
        var message = new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Content = content,
            ProviderId = ProviderId,
            ModelName = ModelName
        };
        _messages.Add(message);
        UpdatedAt = DateTimeOffset.UtcNow;
        return message;
    }

    /// <inheritdoc />
    public async Task<Res<AIChatMessage>> SendMessageAsync(string message, CancellationToken ct = default)
    {
        var provider = _providerFactory.GetProvider(ProviderId);
        if (provider == null)
        {
            return Res.Fail($"Provider '{ProviderId}' not found");
        }

        // 添加用户消息
        AddUserMessage(message);

        try
        {
            var chatClient = provider.GetChatClient();
            var chatMessages = ToChatMessages();

            var response = await chatClient.GetResponseAsync(chatMessages, cancellationToken: ct);
            var responseText = response.Text ?? string.Empty;

            // 添加助手响应
            var assistantMessage = AddAssistantMessage(responseText);

            // 记录 Token 使用量
            if (response.Usage != null)
            {
                assistantMessage.Usage = new TokenUsage
                {
                    InputTokens = (int)(response.Usage.InputTokenCount ?? 0),
                    OutputTokens = (int)(response.Usage.OutputTokenCount ?? 0)
                };
            }

            return assistantMessage;
        }
        catch (Exception ex)
        {
            // 移除失败的用户消息
            _messages.RemoveAt(_messages.Count - 1);
            return Res.Fail($"Failed to send message: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> SendMessageStreamingAsync(
        string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var provider = _providerFactory.GetProvider(ProviderId);
        if (provider == null)
        {
            yield break;
        }

        // 添加用户消息
        AddUserMessage(message);

        // 添加一个占位的助手消息（用于流式更新）
        var assistantMessage = new AIChatMessage
        {
            Role = AIChatRole.Assistant,
            Content = string.Empty,
            ProviderId = ProviderId,
            ModelName = ModelName,
            IsStreaming = true
        };
        _messages.Add(assistantMessage);

        var chatClient = provider.GetChatClient();
        var chatMessages = ToChatMessages();
        // 移除占位消息用于请求
        chatMessages.RemoveAt(chatMessages.Count - 1);

        var fullContent = string.Empty;

        await foreach (var update in chatClient.GetStreamingResponseAsync(chatMessages, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent += update.Text;
                assistantMessage.Content = fullContent;
            }

            yield return update;
        }

        assistantMessage.Content = fullContent;
        assistantMessage.IsStreaming = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public void ClearHistory()
    {
        _messages.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public IList<ChatMessage> ToChatMessages()
    {
        var chatMessages = new List<ChatMessage>();

        // 添加系统提示词
        if (!string.IsNullOrEmpty(SystemPrompt))
        {
            chatMessages.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        }

        // 添加历史消息
        foreach (var msg in _messages)
        {
            chatMessages.Add(msg.ToChatMessage());
        }

        return chatMessages;
    }
}
