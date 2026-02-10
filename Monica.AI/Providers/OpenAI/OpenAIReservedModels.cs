using Monica.AI.Models;

namespace Monica.AI.Providers.OpenAI;

public static class OpenAIReservedModels
{
    public static readonly IReadOnlyList<AIModelInfo> Models =
    [
        new LLMModelInfo
        {
            ModelName = "gpt-4o",
            Description = "OpenAI GPT-4o",
            SupportsImage = true
        },
        new LLMModelInfo
        {
            ModelName = "gpt-4o-mini",
            Description = "OpenAI GPT-4o mini",
            SupportsImage = true
        },
        new LLMModelInfo
        {
            ModelName = "o1",
            Description = "OpenAI O1 reasoning model",
            SupportsReasoning = true
        },
        new LLMModelInfo
        {
            ModelName = "o1-mini",
            Description = "OpenAI O1 mini reasoning model",
            SupportsReasoning = true
        },

        // Embedding models
        new EmbeddingModelInfo
        {
            ModelName = "text-embedding-3-small",
            Description = "OpenAI Text Embedding 3 Small",
            Dimensions = 1536,
            MaxInputTokens = 8191,
            CostPerMillionTokens = 0.02m
        },
        new EmbeddingModelInfo
        {
            ModelName = "text-embedding-3-large",
            Description = "OpenAI Text Embedding 3 Large",
            Dimensions = 3072,
            MaxInputTokens = 8191,
            CostPerMillionTokens = 0.13m
        },
        new EmbeddingModelInfo
        {
            ModelName = "text-embedding-ada-002",
            Description = "OpenAI Text Embedding Ada 002 (Legacy)",
            Dimensions = 1536,
            MaxInputTokens = 8191,
            CostPerMillionTokens = 0.10m
        }
    ];
}
