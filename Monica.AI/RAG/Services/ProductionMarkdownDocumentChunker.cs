using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.AI.RAG.Abstractions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Production-oriented markdown chunker with configurable chunk size and overlap.
/// </summary>
internal sealed partial class ProductionMarkdownDocumentChunker(
    IOptions<ModuleRAGOption> options) : IDocumentChunker
{
    private readonly ModuleRAGOption _options = options.Value;
    private static readonly char[] SentenceBoundaryChars = ['.', '!', '?', '。', '！', '？', '\n'];

    public string ChunkerId => "markdown-production";

    public string DisplayName => "Production Markdown Chunker";

    public string? Description =>
        "Heading-aware semantic chunker with paragraph windows and configurable overlap.";

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

        var maxChunkChars = Math.Max(_options.ProductionChunkerMaxChars, 300);
        var targetChunkChars = Math.Clamp(
            _options.ProductionChunkerTargetChars,
            _options.ProductionChunkerMinChars,
            maxChunkChars);
        var minChunkChars = Math.Max(50, Math.Min(_options.ProductionChunkerMinChars, targetChunkChars));
        var overlapChars = Math.Max(0, Math.Min(_options.ProductionChunkerOverlapChars, targetChunkChars / 2));

        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var section in SplitByHeadings(content, documentTitle))
        {
            var paragraphs = ExtractParagraphs(content, section.Start, section.End, maxChunkChars);
            if (paragraphs.Count == 0)
            {
                continue;
            }

            foreach (var range in BuildChunkWindows(
                         content,
                         paragraphs,
                         targetChunkChars,
                         maxChunkChars,
                         minChunkChars,
                         overlapChars))
            {
                if (!TryTrimRange(content, range.Start, range.End, out var trimmed))
                {
                    continue;
                }

                chunks.Add(new DocumentChunk(
                    content[trimmed.Start..trimmed.End],
                    section.SectionPath,
                    chunkIndex++,
                    trimmed.Start,
                    trimmed.End));
            }
        }

        return chunks;
    }

    private static IEnumerable<SectionRange> SplitByHeadings(string content, string documentTitle)
    {
        var stack = new List<(int Level, string Title)>();
        var currentPath = documentTitle;
        var start = 0;

        foreach (Match match in HeadingRegex().Matches(content))
        {
            if (match.Index > start && TryTrimRange(content, start, match.Index, out var sectionRange))
            {
                yield return new SectionRange(sectionRange.Start, sectionRange.End, currentPath);
            }

            var level = match.Groups["hashes"].Value.Length;
            var title = match.Groups["title"].Value.Trim();

            while (stack.Count > 0 && stack[^1].Level >= level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            stack.Add((level, title));
            currentPath = BuildSectionPath(documentTitle, stack);
            start = match.Index;
        }

        if (TryTrimRange(content, start, content.Length, out var tail))
        {
            yield return new SectionRange(tail.Start, tail.End, currentPath);
        }
    }

    private static string BuildSectionPath(string documentTitle, IReadOnlyList<(int Level, string Title)> headings)
    {
        if (headings.Count == 0)
        {
            return documentTitle;
        }

        return string.Join(
            " > ",
            new[] { documentTitle }.Concat(headings.Select(x => x.Title).Where(x => !string.IsNullOrWhiteSpace(x))));
    }

    private static List<TextRange> ExtractParagraphs(
        string content,
        int start,
        int end,
        int maxChunkChars)
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
            AppendParagraphRange(content, segmentStart, segmentEnd, maxChunkChars, paragraphs);
            segmentStart = Math.Min(match.Index + match.Length, end);
            if (segmentStart >= end)
            {
                break;
            }
        }

        if (segmentStart < end)
        {
            AppendParagraphRange(content, segmentStart, end, maxChunkChars, paragraphs);
        }

        return paragraphs;
    }

    private static void AppendParagraphRange(
        string content,
        int start,
        int end,
        int maxChunkChars,
        List<TextRange> paragraphs)
    {
        if (!TryTrimRange(content, start, end, out var paragraph))
        {
            return;
        }

        if (paragraph.End - paragraph.Start <= maxChunkChars)
        {
            paragraphs.Add(paragraph);
            return;
        }

        foreach (var split in SplitOversizedParagraph(content, paragraph.Start, paragraph.End, maxChunkChars))
        {
            paragraphs.Add(split);
        }
    }

    private static IEnumerable<TextRange> SplitOversizedParagraph(
        string content,
        int start,
        int end,
        int maxChunkChars)
    {
        var cursor = start;
        while (cursor < end)
        {
            var candidateEnd = Math.Min(cursor + maxChunkChars, end);
            if (candidateEnd < end)
            {
                var count = candidateEnd - cursor;
                var boundary = content.LastIndexOfAny(SentenceBoundaryChars, candidateEnd - 1, count);
                if (boundary > cursor + 80)
                {
                    candidateEnd = boundary + 1;
                }
            }

            if (TryTrimRange(content, cursor, candidateEnd, out var split))
            {
                yield return split;
            }

            cursor = candidateEnd;
        }
    }

    private static IEnumerable<TextRange> BuildChunkWindows(
        string content,
        IReadOnlyList<TextRange> paragraphs,
        int targetChunkChars,
        int maxChunkChars,
        int minChunkChars,
        int overlapChars)
    {
        var index = 0;
        while (index < paragraphs.Count)
        {
            var start = paragraphs[index].Start;
            var end = paragraphs[index].End;
            var next = index + 1;

            while (next < paragraphs.Count)
            {
                var candidateEnd = paragraphs[next].End;
                var candidateLength = candidateEnd - start;
                if (candidateLength > maxChunkChars)
                {
                    break;
                }

                if (candidateLength <= targetChunkChars || end - start < minChunkChars)
                {
                    end = candidateEnd;
                    next++;
                    continue;
                }

                break;
            }

            if (end - start < minChunkChars && next < paragraphs.Count)
            {
                var candidateEnd = paragraphs[next].End;
                if (candidateEnd - start <= maxChunkChars)
                {
                    end = candidateEnd;
                    next++;
                }
            }

            if (TryTrimRange(content, start, end, out var chunkRange))
            {
                yield return chunkRange;
            }

            if (next >= paragraphs.Count)
            {
                break;
            }

            var overlapTarget = end - overlapChars;
            var nextIndex = next;
            if (overlapChars > 0)
            {
                for (var i = next - 1; i > index; i--)
                {
                    if (paragraphs[i].Start <= overlapTarget)
                    {
                        nextIndex = i;
                        break;
                    }

                    nextIndex = i;
                }
            }

            if (nextIndex <= index)
            {
                nextIndex = next;
            }

            index = nextIndex;
        }
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

    private readonly record struct SectionRange(int Start, int End, string SectionPath);

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<title>.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"(\r?\n){2,}", RegexOptions.Compiled)]
    private static partial Regex ParagraphSplitRegex();
}
