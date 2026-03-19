namespace Monica.AI.Abstractions;

/// <summary>
/// Provides token counting and token-budget truncation for arbitrary text.
/// </summary>
public interface ITokenCountProvider
{
    /// <summary>
    /// Stable provider identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable provider name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether the token count is estimated instead of exact.
    /// </summary>
    bool IsEstimated { get; }

    /// <summary>
    /// Counts tokens for the supplied text.
    /// </summary>
    TokenCountResult CountTokens(string? text);

    /// <summary>
    /// Truncates text so the returned content fits within the requested token budget.
    /// </summary>
    TokenTruncationResult TruncateToMaxTokens(string? text, int maxTokenCount);
}

/// <summary>
/// Token count metadata for a text payload.
/// </summary>
public sealed record TokenCountResult(
    int CharacterCount,
    int Utf8ByteCount,
    int TokenCount);

/// <summary>
/// Result of truncating text to a token budget.
/// </summary>
public sealed record TokenTruncationResult(
    string Text,
    TokenCountResult Original,
    TokenCountResult Returned,
    bool WasTruncated);
