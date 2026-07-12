namespace Monica.AI.Providers.OpenAI;

/// <summary>
/// Metadata keys emitted for configured OpenAI providers.
/// </summary>
public static class OpenAIProviderMetadataKeys
{
    /// <summary>Identifies the configured OpenAI chat API surface.</summary>
    public const string ApiMode = "OpenAIApiMode";

    /// <summary>Identifies the configured Responses conversation-history strategy.</summary>
    public const string ResponsesHistoryMode = "OpenAIResponsesHistoryMode";
}
