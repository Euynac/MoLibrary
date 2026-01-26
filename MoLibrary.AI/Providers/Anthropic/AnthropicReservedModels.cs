using MoLibrary.AI.Models;

namespace MoLibrary.AI.Providers.Anthropic;

public static class AnthropicReservedModels
{
    public static readonly IReadOnlyList<AIModelInfo> Models =
    [
        new LLMModelInfo
        {
            ModelName = "claude-sonnet-4-20250514",
            Description = "Anthropic Claude Sonnet 4",
            SupportsImage = true
        },
        new LLMModelInfo
        {
            ModelName = "claude-opus-4-20250514",
            Description = "Anthropic Claude Opus 4",
            SupportsImage = true
        },
        new LLMModelInfo
        {
            ModelName = "claude-3-5-sonnet-20241022",
            Description = "Anthropic Claude 3.5 Sonnet",
            SupportsImage = true
        },
        new LLMModelInfo
        {
            ModelName = "claude-3-5-haiku-20241022",
            Description = "Anthropic Claude 3.5 Haiku",
            SupportsImage = true
        }
    ];
}
