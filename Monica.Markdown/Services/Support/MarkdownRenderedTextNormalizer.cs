using System.Text;
using System.Text.RegularExpressions;

namespace Monica.Markdown.Services.Support;

/// <summary>
/// Normalizes markdown fragments to the plain-text form rendered in the UI.
/// </summary>
public static partial class MarkdownRenderedTextNormalizer
{
    public static string NormalizeFragment(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        using var reader = new StringReader(markdown);
        var builder = new StringBuilder();
        var insideCodeFence = false;

        while (reader.ReadLine() is { } line)
        {
            if (IsFenceDelimiter(line))
            {
                insideCodeFence = !insideCodeFence;
                continue;
            }

            var normalizedLine = NormalizeLine(line, insideCodeFence);
            if (string.IsNullOrWhiteSpace(normalizedLine))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(normalizedLine);
        }

        return builder.ToString();
    }

    private static bool IsFenceDelimiter(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
               || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static string NormalizeLine(string line, bool insideCodeFence)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        if (insideCodeFence)
        {
            return MarkdownDocumentSearchText.Normalize(line);
        }

        var value = line.Trim();
        if (HorizontalRulePattern().IsMatch(value))
        {
            return string.Empty;
        }

        value = BlockquotePattern().Replace(value, string.Empty);
        value = UnorderedListPattern().Replace(value, string.Empty);
        value = OrderedListPattern().Replace(value, string.Empty);
        value = ImagePattern().Replace(value, "$1");
        value = LinkPattern().Replace(value, "$1");
        value = ReferenceLinkPattern().Replace(value, "$1");
        value = InlineCodePattern().Replace(value, "$1");
        value = HtmlTagPattern().Replace(value, " ");
        value = value.Replace('|', ' ');
        value = value.Replace("**", string.Empty, StringComparison.Ordinal);
        value = value.Replace("__", string.Empty, StringComparison.Ordinal);
        value = value.Replace('*', ' ');
        value = value.Replace('_', ' ');
        value = value.Replace("~~", string.Empty, StringComparison.Ordinal);

        return MarkdownDocumentSearchText.Normalize(value);
    }

    [GeneratedRegex(@"^\s{0,3}>\s?", RegexOptions.Compiled)]
    private static partial Regex BlockquotePattern();

    [GeneratedRegex(@"^\s*[-+*]\s+", RegexOptions.Compiled)]
    private static partial Regex UnorderedListPattern();

    [GeneratedRegex(@"^\s*\d+\.\s+", RegexOptions.Compiled)]
    private static partial Regex OrderedListPattern();

    [GeneratedRegex(@"!\[(.*?)\]\((.*?)\)", RegexOptions.Compiled)]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[(.*?)\]\((.*?)\)", RegexOptions.Compiled)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\[(.*?)\]\[(.*?)\]", RegexOptions.Compiled)]
    private static partial Regex ReferenceLinkPattern();

    [GeneratedRegex(@"`([^`]*)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"^\s*([-*_]\s*){3,}$", RegexOptions.Compiled)]
    private static partial Regex HorizontalRulePattern();
}
