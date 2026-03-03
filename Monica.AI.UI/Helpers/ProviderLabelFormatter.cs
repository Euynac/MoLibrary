namespace Monica.AI.UI.Helpers;

/// <summary>
/// Formats provider labels for UI display.
/// </summary>
public static class ProviderLabelFormatter
{
    /// <summary>
    /// Formats a provider label using type and ID.
    /// </summary>
    public static string Format(string? providerType, string? providerId)
    {
        var normalizedType = providerType?.Trim();
        var normalizedId = providerId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return normalizedId ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(normalizedId)
            || string.Equals(normalizedType, normalizedId, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedType;
        }

        return $"{normalizedType} ({normalizedId})";
    }

    /// <summary>
    /// Formats provider label with normalization for auto-generated provider IDs.
    /// Example: "openai-qwen3-235b-instruct" -> "OpenAI (qwen3)".
    /// </summary>
    public static string FormatNormalized(string? providerType, string? providerId)
    {
        var normalizedId = providerId?.Trim();
        var fromProviderId = FormatFromProviderId(normalizedId);

        if (!string.IsNullOrWhiteSpace(normalizedId)
            && !string.Equals(fromProviderId, normalizedId, StringComparison.OrdinalIgnoreCase))
        {
            return fromProviderId;
        }

        return Format(providerType, normalizedId);
    }

    /// <summary>
    /// Formats a provider label preferring display name over raw provider ID.
    /// </summary>
    public static string FormatWithDisplayName(
        string? providerType,
        string? providerId,
        string? providerDisplayName)
    {
        return FormatNormalized(providerType, providerId);
    }

    /// <summary>
    /// Best-effort fallback formatter from a raw provider ID.
    /// Used when provider metadata is unavailable.
    /// </summary>
    public static string FormatFromProviderId(string? providerId)
    {
        var normalized = providerId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.StartsWith("openai-", StringComparison.OrdinalIgnoreCase))
        {
            var compact = ExtractIdentity(normalized["openai-".Length..]);
            return Format("OpenAI", compact);
        }

        if (normalized.StartsWith("anthropic-", StringComparison.OrdinalIgnoreCase))
        {
            var compact = ExtractIdentity(normalized["anthropic-".Length..]);
            return Format("Anthropic", compact);
        }

        if (normalized.StartsWith("azure-openai-", StringComparison.OrdinalIgnoreCase))
        {
            var compact = ExtractIdentity(normalized["azure-openai-".Length..]);
            return Format("AzureOpenAI", compact);
        }

        return normalized;
    }

    private static string ExtractIdentity(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        var token = raw
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(token) ? raw : token;
    }
}
