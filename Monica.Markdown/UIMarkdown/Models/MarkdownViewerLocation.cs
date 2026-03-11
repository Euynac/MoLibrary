using Microsoft.AspNetCore.WebUtilities;

namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Represents the shareable URL state of the markdown viewer page.
/// </summary>
public sealed record MarkdownViewerLocation(
    string? GroupKey = null,
    string? DocumentRelativePath = null,
    string? AnchorId = null)
{
    public const string PageUrl = "/markdown-docs";

    private const string GroupQueryKey = "group";
    private const string DocumentQueryKey = "document";

    /// <summary>
    /// Creates the markdown viewer state from an absolute URI.
    /// </summary>
    public static MarkdownViewerLocation FromAbsoluteUri(string absoluteUri)
    {
        var uri = new Uri(absoluteUri, UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(uri.Query);

        return new MarkdownViewerLocation(
            GetQueryValue(query, GroupQueryKey),
            GetQueryValue(query, DocumentQueryKey),
            NormalizeAnchorId(uri.Fragment));
    }

    /// <summary>
    /// Builds a relative URI for the markdown viewer route.
    /// </summary>
    public string ToRelativeUri()
    {
        var query = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(GroupKey))
        {
            query[GroupQueryKey] = GroupKey;
        }

        if (!string.IsNullOrWhiteSpace(DocumentRelativePath))
        {
            query[DocumentQueryKey] = DocumentRelativePath;
        }

        var uri = query.Count == 0
            ? PageUrl
            : QueryHelpers.AddQueryString(PageUrl, query);

        return string.IsNullOrWhiteSpace(AnchorId)
            ? uri
            : $"{uri}#{AnchorId}";
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> query, string key)
    {
        if (!query.TryGetValue(key, out var values))
        {
            return null;
        }

        var value = values.Count > 0 ? values[0] : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeAnchorId(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var anchorId = fragment.Trim();
        if (anchorId.StartsWith('#'))
        {
            anchorId = anchorId[1..];
        }

        return string.IsNullOrWhiteSpace(anchorId) ? null : anchorId;
    }
}
