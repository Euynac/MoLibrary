using System.Text;
using System.Text.RegularExpressions;

namespace Monica.AI.RAG.Models;

/// <summary>
/// Defines normalization and validation rules for knowledge base business IDs.
/// </summary>
public static partial class KnowledgeBaseIdPolicy
{
    public const int MaxLength = 64;

    public static string Normalize(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool IsValid(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length is > 0 and <= MaxLength
               && KnowledgeBaseIdRegex().IsMatch(normalized);
    }

    public static string CreateCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = true;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' || char.IsDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || lastWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            lastWasSeparator = true;
        }

        var candidate = builder.ToString().Trim('-');
        if (candidate.Length <= MaxLength)
        {
            return candidate;
        }

        return candidate[..MaxLength].TrimEnd('-');
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex KnowledgeBaseIdRegex();
}
