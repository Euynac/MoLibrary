using System.Text;
using Monica.AI.Abstractions;

namespace Monica.AI.Services;

/// <summary>
/// Estimates token counts from UTF-8 byte length and supports budget-aware truncation.
/// </summary>
public sealed class EstimatedUtf8TokenCountProvider : ITokenCountProvider
{
    public string ProviderId => "utf8-estimate";

    public string DisplayName => "UTF-8 byte estimate";

    public bool IsEstimated => true;

    public TokenCountResult CountTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TokenCountResult(0, 0, 0);
        }

        var characterCount = text.Length;
        var utf8ByteCount = Encoding.UTF8.GetByteCount(text);
        var tokenCount = Math.Max(1, (int)Math.Ceiling(utf8ByteCount / 4d));

        return new TokenCountResult(characterCount, utf8ByteCount, tokenCount);
    }

    public TokenTruncationResult TruncateToMaxTokens(string? text, int maxTokenCount)
    {
        var normalizedText = text ?? string.Empty;
        var original = CountTokens(normalizedText);
        if (maxTokenCount <= 0 || original.TokenCount <= maxTokenCount)
        {
            return new TokenTruncationResult(
                normalizedText,
                original,
                original,
                WasTruncated: false);
        }

        var upperBound = normalizedText.Length;
        var lowerBound = 0;

        while (lowerBound < upperBound)
        {
            var candidateLength = (lowerBound + upperBound + 1) / 2;
            var candidate = normalizedText[..candidateLength];

            if (CountTokens(candidate).TokenCount <= maxTokenCount)
            {
                lowerBound = candidateLength;
            }
            else
            {
                upperBound = candidateLength - 1;
            }
        }

        var truncatedText = normalizedText[..lowerBound].TrimEnd();
        var returned = CountTokens(truncatedText);

        return new TokenTruncationResult(
            truncatedText,
            original,
            returned,
            WasTruncated: lowerBound < normalizedText.Length);
    }
}
