using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using ChatCompletionOptions = OpenAI.Chat.ChatCompletionOptions;

namespace Monica.AI.Providers.OpenAI;

/// <summary>
/// Applies OpenAI-specific request options that Microsoft.Extensions.AI does not expose directly.
/// </summary>
internal sealed class OpenAIRequestOptionsChatClient(
    IChatClient innerClient,
    OpenAIProviderApiMode apiMode,
    OpenAIResponsesHistoryMode responsesHistoryMode,
    string? promptCacheKey,
    OpenAIPromptCacheRetention? promptCacheRetention)
    : DelegatingChatClient(innerClient)
{
    private readonly OpenAIProviderApiMode _apiMode = apiMode;
    private readonly OpenAIResponsesHistoryMode _responsesHistoryMode = responsesHistoryMode;
    private readonly string? _promptCacheKey = NormalizePromptCacheKey(promptCacheKey);
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
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = base.GetStreamingResponseAsync(
                messages,
                ConfigureOptions(options),
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        var hasEmittedContent = false;

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (ArgumentOutOfRangeException ex) when (CanCompleteAfterUnknownReasoningStatus(
                    ex,
                    hasEmittedContent))
                {
                    // Some OpenAI-compatible Responses endpoints emit an empty or non-standard
                    // reasoning status on output_item.done. Text deltas are already complete at
                    // this point, so treat that malformed terminal metadata as end-of-stream.
                    yield break;
                }

                if (!hasNext)
                {
                    break;
                }

                var update = enumerator.Current;
                hasEmittedContent |= update.Contents.Any(static content => content switch
                {
                    TextContent text => !string.IsNullOrEmpty(text.Text),
                    TextReasoningContent reasoning => !string.IsNullOrEmpty(reasoning.Text),
                    _ => false
                });
                yield return update;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    private bool CanCompleteAfterUnknownReasoningStatus(
        ArgumentOutOfRangeException exception,
        bool hasEmittedContent)
    {
        return _apiMode == OpenAIProviderApiMode.Responses
               && hasEmittedContent
               && string.Equals(exception.ParamName, "value", StringComparison.Ordinal)
               && exception.Message.Contains("Unknown ReasoningStatus value.", StringComparison.Ordinal);
    }

    private ChatOptions ConfigureOptions(ChatOptions? options)
    {
        var configuredOptions = options?.Clone() ?? new ChatOptions();
        var previousFactory = configuredOptions.RawRepresentationFactory;

        configuredOptions.RawRepresentationFactory = chatClient =>
        {
            var rawOptions = previousFactory?.Invoke(chatClient);
#pragma warning disable OPENAI001
            object configuredRawOptions = _apiMode switch
            {
                OpenAIProviderApiMode.Responses => ConfigureResponseOptions(
                    rawOptions as CreateResponseOptions ?? new CreateResponseOptions()),
                OpenAIProviderApiMode.Chat => ConfigureChatCompletionOptions(
                    rawOptions as ChatCompletionOptions ?? new ChatCompletionOptions()),
                _ => throw new InvalidOperationException($"Unsupported OpenAI API mode '{_apiMode}'.")
            };
#pragma warning restore OPENAI001

            return configuredRawOptions;
        };

        return configuredOptions;
    }

#pragma warning disable OPENAI001
#pragma warning disable SCME0001
    private CreateResponseOptions ConfigureResponseOptions(CreateResponseOptions options)
    {
        if (_responsesHistoryMode == OpenAIResponsesHistoryMode.LocalHistory)
        {
            options.StoredOutputEnabled = false;
        }

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
        if (_promptCacheKey is null)
        {
            return;
        }

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

    private static string? NormalizePromptCacheKey(string? promptCacheKey)
    {
        return string.IsNullOrWhiteSpace(promptCacheKey) ? null : promptCacheKey.Trim();
    }
}
