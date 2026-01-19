using Anthropic;
using Microsoft.Extensions.AI;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AI.Providers.Anthropic;

/// <summary>
/// Anthropic Provider 实现
/// </summary>
public class AnthropicProvider : IAIProvider
{
    private readonly AnthropicProviderOptions _options;
    private readonly AnthropicClient _anthropicClient;
    private readonly IChatClient _chatClient;
    private bool _disposed;

    public AnthropicProvider(AnthropicProviderOptions options)
    {
        _options = options;

        // Anthropic SDK v12 使用对象初始化器配置客户端
        _anthropicClient = new AnthropicClient { ApiKey = options.ApiKey };
        // Anthropic SDK v12 官方实现 IChatClient
        _chatClient = _anthropicClient.AsIChatClient(options.Model);
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? $"anthropic-{_options.Model}";

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? $"Anthropic ({_options.Model})";

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = "Anthropic Claude models",
        ProviderType = "Anthropic",
        DefaultModel = _options.Model,
        SupportsStreaming = true,
        SupportsFunctionCalling = true,
        IsDefault = _options.IsDefault,
        Icon = "anthropic"
    };

    /// <inheritdoc />
    public IChatClient GetChatClient() => _chatClient;

    /// <inheritdoc />
    public async Task<Res> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { MaxOutputTokens = 10 },
                ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"Anthropic connection test failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Res<IReadOnlyList<string>>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        // 返回 Anthropic 常用模型列表
        var models = new List<string>
        {
            "claude-sonnet-4-20250514",
            "claude-opus-4-20250514",
            "claude-3-5-sonnet-20241022",
            "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307"
        };
        return Task.FromResult(Res.Ok<IReadOnlyList<string>>(models));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                (_chatClient as IDisposable)?.Dispose();
            }
            _disposed = true;
        }
    }
}
