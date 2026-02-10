using System.Text;
using System.Text.RegularExpressions;
using Monica.AI.RAG.Abstractions;

namespace Monica.AI.RAG.Services;

/// <summary>
/// Splits markdown documents by heading structure.
/// </summary>
public partial class MarkdownDocumentChunker : IDocumentChunker
{
    private const int MaxChunkChars = 1500;

    public IReadOnlyList<string> SupportedExtensions => [".md", ".markdown"];

    public IReadOnlyList<DocumentChunk> ChunkDocument(
        string content, string documentPath, string documentTitle)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var sections = SplitByHeadings(content, documentTitle);
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var section in sections)
        {
            if (section.Content.Length <= MaxChunkChars)
            {
                chunks.Add(new DocumentChunk(
                    section.Content.Trim(),
                    section.SectionPath,
                    chunkIndex++));
            }
            else
            {
                // Split oversized sections by paragraphs
                foreach (var part in SplitByParagraphs(section.Content))
                {
                    chunks.Add(new DocumentChunk(
                        part.Trim(),
                        section.SectionPath,
                        chunkIndex++));
                }
            }
        }

        return chunks;
    }

    private static List<(string Content, string? SectionPath)> SplitByHeadings(
        string content, string documentTitle)
    {
        var sections = new List<(string Content, string? SectionPath)>();
        var lines = content.Split('\n');
        var currentContent = new StringBuilder();
        string? currentH2 = null;
        string? currentH3 = null;

        foreach (var line in lines)
        {
            var h2Match = H2Regex().Match(line);
            var h3Match = H3Regex().Match(line);

            if (h2Match.Success)
            {
                FlushSection(sections, currentContent, documentTitle, currentH2, currentH3);
                currentH2 = h2Match.Groups[1].Value.Trim();
                currentH3 = null;
                currentContent.AppendLine(line);
            }
            else if (h3Match.Success)
            {
                FlushSection(sections, currentContent, documentTitle, currentH2, currentH3);
                currentH3 = h3Match.Groups[1].Value.Trim();
                currentContent.AppendLine(line);
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        FlushSection(sections, currentContent, documentTitle, currentH2, currentH3);
        return sections;
    }

    private static void FlushSection(
        List<(string Content, string? SectionPath)> sections,
        StringBuilder content, string documentTitle,
        string? h2, string? h3)
    {
        var text = content.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            content.Clear();
            return;
        }

        var path = BuildSectionPath(documentTitle, h2, h3);
        sections.Add((text, path));
        content.Clear();
    }

    private static string BuildSectionPath(string documentTitle, string? h2, string? h3)
    {
        if (h2 is null) return documentTitle;
        if (h3 is null) return $"{documentTitle} > {h2}";
        return $"{documentTitle} > {h2} > {h3}";
    }

    private static IEnumerable<string> SplitByParagraphs(string content)
    {
        var paragraphs = ParagraphSplitRegex().Split(content);
        var buffer = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            if (buffer.Length + paragraph.Length > MaxChunkChars && buffer.Length > 0)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }

            if (buffer.Length > 0)
                buffer.AppendLine();

            buffer.Append(paragraph.Trim());
        }

        if (buffer.Length > 0)
            yield return buffer.ToString();
    }

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex H2Regex();

    [GeneratedRegex(@"^###\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex H3Regex();

    [GeneratedRegex(@"\n\s*\n", RegexOptions.Compiled)]
    private static partial Regex ParagraphSplitRegex();
}