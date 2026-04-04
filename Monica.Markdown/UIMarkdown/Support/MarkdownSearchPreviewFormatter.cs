using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.UIMarkdown.Support;

/// <summary>
/// Splits a search result preview into highlighted and non-highlighted segments for rendering.
/// </summary>
public static class MarkdownSearchPreviewFormatter
{
    /// <summary>
    /// Builds preview parts in display order.
    /// </summary>
    public static IReadOnlyList<MarkdownSearchPreviewPart> BuildParts(MarkdownDocumentSearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.PreviewHighlights.Count == 0)
        {
            return [new MarkdownSearchPreviewPart(result.PreviewText, false)];
        }

        var parts = new List<MarkdownSearchPreviewPart>();
        var cursor = 0;

        foreach (var highlight in result.PreviewHighlights
                     .OrderBy(static segment => segment.Start))
        {
            var safeStart = Math.Clamp(highlight.Start, 0, result.PreviewText.Length);
            var safeLength = Math.Clamp(highlight.Length, 0, result.PreviewText.Length - safeStart);
            if (safeLength == 0)
            {
                continue;
            }

            if (safeStart > cursor)
            {
                parts.Add(new MarkdownSearchPreviewPart(result.PreviewText[cursor..safeStart], false));
            }

            parts.Add(new MarkdownSearchPreviewPart(
                result.PreviewText.Substring(safeStart, safeLength),
                true));

            cursor = safeStart + safeLength;
        }

        if (cursor < result.PreviewText.Length)
        {
            parts.Add(new MarkdownSearchPreviewPart(result.PreviewText[cursor..], false));
        }

        return parts;
    }
}

/// <summary>
/// Renderable preview segment for the markdown search dialog.
/// </summary>
public sealed record MarkdownSearchPreviewPart(string Text, bool IsMatch);
