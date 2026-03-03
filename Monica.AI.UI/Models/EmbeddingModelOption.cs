namespace Monica.AI.UI.Models;

/// <summary>
/// UI model describing a selectable embedding model under a provider.
/// </summary>
public sealed record EmbeddingModelOption
{
    public required string ProviderId { get; init; }
    public required string ProviderDisplayName { get; init; }
    public required string ModelName { get; init; }
    public int? Dimensions { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Stable key used by selectors in UI.
    /// </summary>
    public string ModelKey => ToModelKey(ProviderId, ModelName);

    /// <summary>
    /// Builds a stable model key for provider/model pair.
    /// </summary>
    public static string ToModelKey(string providerId, string modelName)
        => $"{providerId}::{modelName}";

    /// <summary>
    /// Parses a model key into provider/model pair.
    /// </summary>
    public static bool TryParseModelKey(
        string? modelKey,
        out string providerId,
        out string modelName)
    {
        providerId = string.Empty;
        modelName = string.Empty;

        if (string.IsNullOrWhiteSpace(modelKey))
        {
            return false;
        }

        var separatorIndex = modelKey.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= modelKey.Length - 2)
        {
            return false;
        }

        providerId = modelKey[..separatorIndex];
        modelName = modelKey[(separatorIndex + 2)..];
        return true;
    }
}
