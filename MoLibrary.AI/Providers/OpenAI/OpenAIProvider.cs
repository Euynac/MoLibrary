using System.ClientModel;
using Microsoft.Extensions.AI;
using MoLibrary.AI.Abstractions;
using MoLibrary.AI.Models;
using MoLibrary.Tool.MoResponse;
using OpenAI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace MoLibrary.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Provider 实现
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly OpenAIProviderOptions _options;
    private readonly IChatClient _chatClient;
    private bool _disposed;

    public OpenAIProvider(OpenAIProviderOptions options)
    {
        _options = options;

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(options.BaseUrl);
        }

        var openAiClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        _chatClient = openAiClient.GetChatClient(options.Model).AsIChatClient();
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId ?? $"openai-{_options.Model}";

    /// <inheritdoc />
    public string DisplayName => _options.DisplayName ?? $"OpenAI ({_options.Model})";

    /// <inheritdoc />
    public AIProviderInfo Info => new()
    {
        ProviderId = ProviderId,
        DisplayName = DisplayName,
        Description = "OpenAI GPT models",
        ProviderType = "OpenAI",
        DefaultModel = _options.Model,
        SupportsStreaming = true,
        SupportsFunctionCalling = true,
        IsDefault = _options.IsDefault,
        Icon = "openai"
    };

    /// <inheritdoc />
    public IChatClient GetChatClient() => _chatClient;

    /// <inheritdoc />
    public async Task<Res> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _chatClient.GetResponseAsync(
                [new AIChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { MaxOutputTokens = 10 },
                ct);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"OpenAI connection test failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Res<IReadOnlyList<string>>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        // OpenAI 目前没有通过 SDK 获取模型列表的简单方法
        // 返回常用模型列表
        var models = new List<string>
        {
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4-turbo",
            "gpt-4",
            "gpt-3.5-turbo",
            "o1",
            "o1-mini",
            "o1-preview",
            "o3-mini"
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
