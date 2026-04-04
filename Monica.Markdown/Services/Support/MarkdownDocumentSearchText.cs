using System.Globalization;
using System.Text;
using Monica.Markdown.Models;

namespace Monica.Markdown.Services.Support;

internal static class MarkdownDocumentSearchText
{
    private static readonly CompareInfo CompareInfo = CultureInfo.InvariantCulture.CompareInfo;

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousWasWhitespace = true;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> SplitKeywords(string normalizedQuery)
    {
        return normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int IndexOfIgnoreCase(string source, string value, int startIndex = 0)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value) || startIndex >= source.Length)
        {
            return -1;
        }

        return CompareInfo.IndexOf(source, value, startIndex, CompareOptions.IgnoreCase);
    }

    public static bool ContainsIgnoreCase(string source, string value)
    {
        return IndexOfIgnoreCase(source, value) >= 0;
    }

    public static IReadOnlyList<MarkdownSearchMatchSegment> FindAllSegments(
        string source,
        IEnumerable<string> values)
    {
        if (string.IsNullOrEmpty(source))
        {
            return [];
        }

        var segments = new List<MarkdownSearchMatchSegment>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var index = 0;
            while (index < source.Length)
            {
                var matchIndex = IndexOfIgnoreCase(source, value, index);
                if (matchIndex < 0)
                {
                    break;
                }

                segments.Add(new MarkdownSearchMatchSegment(matchIndex, value.Length));
                index = matchIndex + value.Length;
            }
        }

        return MergeSegments(segments);
    }

    public static IReadOnlyList<MarkdownSearchMatchSegment> MergeSegments(
        IEnumerable<MarkdownSearchMatchSegment> segments)
    {
        var orderedSegments = segments
            .Where(static segment => segment.Length > 0)
            .OrderBy(static segment => segment.Start)
            .ThenByDescending(static segment => segment.Length)
            .ToArray();

        if (orderedSegments.Length == 0)
        {
            return [];
        }

        var merged = new List<MarkdownSearchMatchSegment> { orderedSegments[0] };

        foreach (var segment in orderedSegments.Skip(1))
        {
            var current = merged[^1];
            var currentEnd = current.Start + current.Length;
            var segmentEnd = segment.Start + segment.Length;

            if (segment.Start <= currentEnd)
            {
                merged[^1] = current with { Length = Math.Max(currentEnd, segmentEnd) - current.Start };
                continue;
            }

            merged.Add(segment);
        }

        return merged;
    }

    public static bool TryFindLooseSubsequence(
        string source,
        string normalizedQuery,
        out MarkdownSearchMatchSegment span,
        out double compactness)
    {
        span = new MarkdownSearchMatchSegment(0, 0);
        compactness = 0;

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return false;
        }

        var positions = new List<int>(normalizedQuery.Length);
        var searchStart = 0;

        foreach (var queryCharacter in normalizedQuery)
        {
            var index = FindNextCharacter(source, queryCharacter, searchStart);
            if (index < 0)
            {
                return false;
            }

            positions.Add(index);
            searchStart = index + 1;
        }

        var start = positions[0];
        var length = positions[^1] - start + 1;
        span = new MarkdownSearchMatchSegment(start, length);
        compactness = normalizedQuery.Length / (double)length;
        return true;
    }

    private static int FindNextCharacter(string source, char queryCharacter, int startIndex)
    {
        for (var index = startIndex; index < source.Length; index++)
        {
            if (char.ToUpperInvariant(source[index]) == char.ToUpperInvariant(queryCharacter))
            {
                return index;
            }
        }

        return -1;
    }
}
