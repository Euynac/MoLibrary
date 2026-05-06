using Microsoft.Extensions.AI;
using OpenAI.Responses;
using ChatCompletionOptions = OpenAI.Chat.ChatCompletionOptions;

namespace Monica.AI.Providers.OpenAI;

/// <summary>
/// Adds OpenAI prompt cache request fields that Microsoft.Extensions.AI does not expose directly.
/// </summary>
internal sealed class OpenAIPromptCacheChatClient(
    IChatClient innerClient,
    OpenAIProviderApiMode apiMode,
    string promptCacheKey,
    OpenAIPromptCacheRetention? promptCacheRetention)
    : DelegatingChatClient(innerClient)
{
    private readonly OpenAIProviderApiMode _apiMode = apiMode;
    private readonly string _promptCacheKey = promptCacheKey;
    private readonly string? _promptCacheRetention = ConvertRetention(promptCacheRetention);

    /// <inheritdoc />
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(messages, ConfigureOptions(options), cancellationToken);
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(messages, ConfigureOptions(options), cancellationToken);
    }

    private ChatOptions ConfigureOptions(ChatOptions? options)
    {
        var configuredOptions = options?.Clone() ?? new ChatOptions();
        var previousFactory = configuredOptions.RawRepresentationFactory;

        configuredOptions.RawRepresentationFactory = chatClient =>
        {
            var rawOptions = previousFactory?.Invoke(chatClient);
#pragma warning disable OPENAI001
            var configuredRawOptions = rawOptions switch
            {
                CreateResponseOptions responseOptions => ConfigureResponseOptions(responseOptions),
                ChatCompletionOptions chatOptions => ConfigureChatCompletionOptions(chatOptions),
                null => CreateDefaultRawOptions(),
                _ => rawOptions
            };
#pragma warning restore OPENAI001

            return configuredRawOptions;
        };

        return configuredOptions;
    }

#pragma warning disable OPENAI001
#pragma warning disable SCME0001
    private object CreateDefaultRawOptions()
    {
        return _apiMode switch
        {
            OpenAIProviderApiMode.Responses => ConfigureResponseOptions(new CreateResponseOptions()),
            OpenAIProviderApiMode.Chat => ConfigureChatCompletionOptions(new ChatCompletionOptions()),
            _ => throw new InvalidOperationException($"Unsupported OpenAI API mode '{_apiMode}'.")
        };
    }

    private CreateResponseOptions ConfigureResponseOptions(CreateResponseOptions options)
    {
        SetPromptCacheFields(options.Patch);
        return options;
    }

    private ChatCompletionOptions ConfigureChatCompletionOptions(ChatCompletionOptions options)
    {
        SetPromptCacheFields(options.Patch);
        return options;
    }

    private void SetPromptCacheFields(System.ClientModel.Primitives.JsonPatch patch)
    {
        patch.Set("$.prompt_cache_key"u8, _promptCacheKey);
        if (_promptCacheRetention is not null)
        {
            patch.Set("$.prompt_cache_retention"u8, _promptCacheRetention);
        }
    }
#pragma warning restore SCME0001
#pragma warning restore OPENAI001

    private static string? ConvertRetention(OpenAIPromptCacheRetention? retention)
    {
        return retention switch
        {
            OpenAIPromptCacheRetention.InMemory => "in_memory",
            OpenAIPromptCacheRetention.TwentyFourHours => "24h",
            null => null,
            _ => throw new InvalidOperationException($"Unsupported OpenAI prompt cache retention '{retention}'.")
        };
    }
}
