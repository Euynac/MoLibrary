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
        }
    ];
}
