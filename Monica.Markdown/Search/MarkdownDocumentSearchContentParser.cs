using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Monica.Markdown.Models;

namespace Monica.Markdown.Search;

internal static partial class MarkdownDocumentSearchContentParser
{
    public static IReadOnlyList<MarkdownDocumentSearchSection> ParseSections(
        MarkdownDocument document,
        string markdown)
    {
        var content = StripFrontMatter(markdown);
        var sections = new List<MarkdownDocumentSearchSection>();
        var headingTrail = new List<HeadingFrame>();
        var buffer = new List<string>();

        var currentTitle = document.Title;
        string? currentAnchorId = null;
        int? currentHeadingLevel = null;
        var currentIsDocumentSection = true;
        var insideCodeFence = false;

        foreach (var rawLine in EnumerateLines(content))
        {
            if (IsFenceDelimiter(rawLine))
            {
                insideCodeFence = !insideCodeFence;
                continue;
            }

            if (!insideCodeFence && TryParseHeading(rawLine, out var headingLevel, out var headingTitle))
            {
                sections.Add(CreateSection(
                    currentTitle,
                    headingTrail,
                    buffer,
                    currentAnchorId,
                    currentHeadingLevel,
                    currentIsDocumentSection));

                buffer.Clear();

                while (headingTrail.Count > 0 && headingTrail[^1].Level >= headingLevel)
                {
                    headingTrail.RemoveAt(headingTrail.Count - 1);
                }

                var anchorId = CreateAnchorId(headingTitle);
                headingTrail.Add(new HeadingFrame(headingLevel, headingTitle));

                currentTitle = headingTitle;
                currentAnchorId = anchorId;
                currentHeadingLevel = headingLevel;
                currentIsDocumentSection = false;
                continue;
            }

            var normalizedLine = NormalizeLine(rawLine, insideCodeFence);
            if (!string.IsNullOrWhiteSpace(normalizedLine))
            {
                buffer.Add(normalizedLine);
            }
        }

        sections.Add(CreateSection(
            currentTitle,
            headingTrail,
            buffer,
            currentAnchorId,
            currentHeadingLevel,
            currentIsDocumentSection));

        return sections
            .Where(static section =>
                section.IsDocumentSection
                || !string.IsNullOrWhiteSpace(section.NormalizedText)
                || !string.IsNullOrWhiteSpace(section.NormalizedHeadingTrail))
            .ToArray();
    }

    private static MarkdownDocumentSearchSection CreateSection(
        string title,
        IReadOnlyList<HeadingFrame> headingTrail,
        IReadOnlyList<string> lines,
        string? anchorId,
        int? headingLevel,
        bool isDocumentSection)
    {
        var headingTrailText = isDocumentSection
            ? string.Empty
            : string.Join(" / ", headingTrail.Select(static heading => heading.Title));
        var text = MarkdownDocumentSearchText.Normalize(string.Join(Environment.NewLine, lines));

        return new MarkdownDocumentSearchSection(
            title,
            MarkdownDocumentSearchText.Normalize(title),
            headingTrailText,
            MarkdownDocumentSearchText.Normalize(headingTrailText),
            text,
            anchorId,
            headingLevel,
            isDocumentSection);
    }

    private static IEnumerable<string> EnumerateLines(string content)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static string StripFrontMatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var span = content.AsSpan().TrimStart();
        if (!span.StartsWith("---"))
        {
            return content;
        }

        var firstLineEnd = span.IndexOfAny('\r', '\n');
        if (firstLineEnd < 0)
        {
            return content;
        }

        var afterFirstLine = firstLineEnd;
        while (afterFirstLine < span.Length && (span[afterFirstLine] is '\r' or '\n'))
        {
            afterFirstLine++;
        }

        var remaining = span[afterFirstLine..];
        var closingIndex = FindClosingDelimiter(remaining);
        if (closingIndex < 0)
        {
            return content;
        }

        var afterClosing = closingIndex;
        while (afterClosing < remaining.Length && remaining[afterClosing] is not '\r' and not '\n')
        {
            afterClosing++;
        }

        while (afterClosing < remaining.Length && (remaining[afterClosing] is '\r' or '\n'))
        {
            afterClosing++;
        }

        return remaining[afterClosing..].ToString();
    }

    private static int FindClosingDelimiter(ReadOnlySpan<char> content)
    {
        var position = 0;
        while (position < content.Length)
        {
            var lineStart = position;
            var lineLength = content[position..].IndexOfAny('\r', '\n');
            if (lineLength < 0)
            {
                lineLength = content.Length - position;
            }

            var line = content.Slice(position, lineLength).Trim();
            if (line is "---" or "...")
            {
                return lineStart;
            }

            position += lineLength;
            while (position < content.Length && (content[position] is '\r' or '\n'))
            {
                position++;
            }
        }

        return -1;
    }

    private static bool IsFenceDelimiter(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
               || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static bool TryParseHeading(string line, out int level, out string title)
    {
        level = default;
        title = string.Empty;

        var match = HeadingPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }

        level = match.Groups["hash"].Value.Length;
        title = NormalizeLine(match.Groups["title"].Value, insideCodeFence: false);
        return !string.IsNullOrWhiteSpace(title);
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

    private static string CreateAnchorId(string headingText)
    {
        var builder = new StringBuilder();
        var remaining = headingText.AsSpan();

        while (!remaining.IsEmpty)
        {
            var spaceIndex = remaining.IndexOf(' ');
            if (spaceIndex < 0)
            {
                AppendLowerInvariant(builder, remaining);
                break;
            }

            if (spaceIndex > 0)
            {
                AppendLowerInvariant(builder, remaining[..spaceIndex]);
                builder.Append('-');
            }

            remaining = remaining[(spaceIndex + 1)..];
        }

        return WebUtility.UrlEncode(builder.ToString());
    }

    private static void AppendLowerInvariant(StringBuilder builder, ReadOnlySpan<char> segment)
    {
        foreach (var character in segment)
        {
            builder.Append(char.ToLowerInvariant(character));
        }
    }

    [GeneratedRegex(@"^\s*(?<hash>#{1,6})\s+(?<title>.*?)\s*$", RegexOptions.Compiled)]
    private static partial Regex HeadingPattern();

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

    private sealed record HeadingFrame(int Level, string Title);
}
