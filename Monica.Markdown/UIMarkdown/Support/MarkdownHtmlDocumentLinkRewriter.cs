using System.Text.RegularExpressions;
using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.UIMarkdown.Support;

/// <summary>
/// Rewrites raw HTML anchor tags inside markdown source so local document links stay within the markdown viewer.
/// </summary>
internal static partial class MarkdownHtmlDocumentLinkRewriter
{
    /// <summary>
    /// Rewrites local markdown document links contained in raw HTML anchor tags.
    /// </summary>
    public static string? Rewrite(
        string? content,
        string? groupKey,
        string? currentDocumentRelativePath,
        string? currentCulture)
    {
        if (string.IsNullOrWhiteSpace(content)
            || string.IsNullOrWhiteSpace(groupKey)
            || string.IsNullOrWhiteSpace(currentDocumentRelativePath))
        {
            return content;
        }

        return AnchorHrefRegex().Replace(content, match =>
        {
            var quote = match.Groups["quote"].Value;
            var originalUrl = match.Groups["url"].Value;
            var rewrittenUrl = MarkdownViewerLocation.TryResolveDocumentLink(
                groupKey,
                currentDocumentRelativePath,
                currentCulture,
                originalUrl);

            return string.IsNullOrWhiteSpace(rewrittenUrl)
                ? match.Value
                : $"{match.Groups["prefix"].Value}{quote}{rewrittenUrl}{quote}";
        });
    }

    [GeneratedRegex(
        "(?<prefix><a\\b[^>]*?\\bhref\\s*=\\s*)(?<quote>['\"])(?<url>[^'\"]+)\\k<quote>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorHrefRegex();
}
