using System.Text.RegularExpressions;
using Monica.AI.RAG.Abstractions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Lightweight markdown chunker for baseline testing.
/// Splits by heading sections and then by paragraph windows.
/// </summary>
public partial class SimpleMarkdownDocumentChunker : IDocumentChunker
{
    private const int MaxChunkChars = 1500;

    public string ChunkerId => "markdown-simple";

    public string DisplayName => "Simple Markdown Chunker";

    public string? Description =>
        "Heading + paragraph splitter intended for quick validation and small documents.";

    public IReadOnlyList<string> SupportedExtensions => [".md", ".markdown"];

    public IReadOnlyList<DocumentChunk> ChunkDocument(
        string content,
        string documentPath,
        string documentTitle)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var section in SplitByHeadings(content, documentTitle))
        {
            var length = section.End - section.Start;
            if (length <= MaxChunkChars)
            {
                if (TryBuildChunk(content, section.Start, section.End, section.SectionPath, chunkIndex, out var chunk))
                {
                    chunks.Add(chunk);
                    chunkIndex++;
                }
                continue;
            }

            foreach (var range in SplitByParagraphs(content, section.Start, section.End, MaxChunkChars))
            {
                if (TryBuildChunk(content, range.Start, range.End, section.SectionPath, chunkIndex, out var chunk))
                {
                    chunks.Add(chunk);
                    chunkIndex++;
                }
            }
        }

        return chunks;
    }

    private static IEnumerable<SectionRange> SplitByHeadings(string content, string documentTitle)
    {
        var matches = HeadingRegex().Matches(content);
        var start = 0;
        string? h2 = null;
        string? h3 = null;

        foreach (Match match in matches)
        {
            if (match.Index > start)
            {
                if (TryTrimRange(content, start, match.Index, out var trimmed))
                {
                    yield return new SectionRange(trimmed.Start, trimmed.End, BuildSectionPath(documentTitle, h2, h3));
                }
            }

            var level = match.Groups["level"].Value;
            var title = match.Groups["title"].Value.Trim();
            if (string.Equals(level, "##", StringComparison.Ordinal))
            {
                h2 = title;
                h3 = null;
            }
            else
            {
                h3 = title;
            }

            start = match.Index;
        }

        if (TryTrimRange(content, start, content.Length, out var finalRange))
        {
            yield return new SectionRange(finalRange.Start, finalRange.End, BuildSectionPath(documentTitle, h2, h3));
        }
    }

    private static IEnumerable<TextRange> SplitByParagraphs(
        string content,
        int start,
        int end,
        int maxChars)
    {
        var paragraphs = new List<TextRange>();
        var segmentStart = start;

        foreach (Match match in ParagraphSplitRegex().Matches(content, start))
        {
            if (match.Index >= end)
            {
                break;
            }

            var segmentEnd = Math.Min(match.Index, end);
            if (TryTrimRange(content, segmentStart, segmentEnd, out var paragraph))
            {
                paragraphs.Add(paragraph);
            }

            segmentStart = Math.Min(match.Index + match.Length, end);
            if (segmentStart >= end)
            {
                break;
            }
        }

        if (segmentStart < end && TryTrimRange(content, segmentStart, end, out var tail))
        {
            paragraphs.Add(tail);
        }

        if (paragraphs.Count == 0)
        {
            yield break;
        }

        var currentStart = -1;
        var currentEnd = -1;
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.End - paragraph.Start > maxChars)
            {
                if (currentStart >= 0)
                {
                    yield return new TextRange(currentStart, currentEnd);
                    currentStart = -1;
                    currentEnd = -1;
                }

                var cursor = paragraph.Start;
                while (cursor < paragraph.End)
                {
                    var chunkEnd = Math.Min(cursor + maxChars, paragraph.End);
                    if (TryTrimRange(content, cursor, chunkEnd, out var forcedRange))
                    {
                        yield return forcedRange;
                    }

                    cursor = chunkEnd;
                }

                continue;
            }

            if (currentStart < 0)
            {
                currentStart = paragraph.Start;
                currentEnd = paragraph.End;
                continue;
            }

            if (paragraph.End - currentStart > maxChars)
            {
                yield return new TextRange(currentStart, currentEnd);
                currentStart = paragraph.Start;
            }

            currentEnd = paragraph.End;
        }

        if (currentStart >= 0 && currentEnd > currentStart)
        {
            yield return new TextRange(currentStart, currentEnd);
        }
    }

    private static string BuildSectionPath(string documentTitle, string? h2, string? h3)
    {
        if (string.IsNullOrWhiteSpace(h2))
        {
            return documentTitle;
        }

        return string.IsNullOrWhiteSpace(h3)
            ? $"{documentTitle} > {h2}"
            : $"{documentTitle} > {h2} > {h3}";
    }

    private static bool TryBuildChunk(
        string content,
        int start,
        int end,
        string? sectionPath,
        int chunkIndex,
        out DocumentChunk chunk)
    {
        if (TryTrimRange(content, start, end, out var trimmed))
        {
            chunk = new DocumentChunk(
                content[trimmed.Start..trimmed.End],
                sectionPath,
                chunkIndex,
                trimmed.Start,
                trimmed.End);
            return true;
        }

        chunk = default!;
        return false;
    }

    private static bool TryTrimRange(string content, int start, int end, out TextRange range)
    {
        var s = Math.Max(0, start);
        var e = Math.Min(content.Length, end);

        while (s < e && char.IsWhiteSpace(content[s]))
        {
            s++;
        }

        while (e > s && char.IsWhiteSpace(content[e - 1]))
        {
            e--;
        }

        if (e <= s)
        {
            range = default;
            return false;
        }

        range = new TextRange(s, e);
        return true;
    }

    private readonly record struct TextRange(int Start, int End);

    private readonly record struct SectionRange(int Start, int End, string? SectionPath);

    [GeneratedRegex(@"^(?<level>##|###)\s+(?<title>.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"(\r?\n){2,}", RegexOptions.Compiled)]
    private static partial Regex ParagraphSplitRegex();
}

/// <summary>
/// Backward-compatible type name for existing registrations.
/// </summary>
[Obsolete("Use SimpleMarkdownDocumentChunker or ProductionMarkdownDocumentChunker instead.")]
public sealed class MarkdownDocumentChunker : SimpleMarkdownDocumentChunker;
