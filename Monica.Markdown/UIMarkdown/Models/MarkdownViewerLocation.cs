using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Converts a local markdown document link into a markdown viewer route.
    /// </summary>
    /// <param name="groupKey">The current document group key.</param>
    /// <param name="currentDocumentRelativePath">The current document path relative to the group root.</param>
    /// <param name="originalUrl">The markdown link URL as authored in the document.</param>
    /// <returns>
    /// A markdown viewer route when the link targets another local markdown document; otherwise <see langword="null" />.
    /// </returns>
    public static string? TryResolveDocumentLink(
        string? groupKey,
        string? currentDocumentRelativePath,
        string? originalUrl)
    {
        if (string.IsNullOrWhiteSpace(groupKey)
            || string.IsNullOrWhiteSpace(currentDocumentRelativePath)
            || !TryParseRelativeMarkdownLink(originalUrl, out var targetDocumentRelativePath, out var anchorId))
        {
            return null;
        }

        var resolvedDocumentRelativePath = ResolveDocumentRelativePath(
            currentDocumentRelativePath,
            targetDocumentRelativePath);

        if (string.IsNullOrWhiteSpace(resolvedDocumentRelativePath))
        {
            return null;
        }

        return new MarkdownViewerLocation(groupKey, resolvedDocumentRelativePath, anchorId).ToRelativeUri();
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

    private static bool TryParseRelativeMarkdownLink(
        string? originalUrl,
        [NotNullWhen(true)] out string? targetDocumentRelativePath,
        out string? anchorId)
    {
        targetDocumentRelativePath = null;
        anchorId = null;

        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return false;
        }

        var trimmed = originalUrl.Trim();
        if (trimmed.StartsWith('#')
            || trimmed.StartsWith('/')
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || Path.IsPathRooted(trimmed)
            || Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return false;
        }

        var queryIndex = trimmed.IndexOf('?');
        if (queryIndex >= 0)
        {
            return false;
        }

        var fragmentIndex = trimmed.IndexOf('#');
        var pathPart = fragmentIndex >= 0 ? trimmed[..fragmentIndex] : trimmed;
        if (string.IsNullOrWhiteSpace(pathPart)
            || !pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetDocumentRelativePath = Uri.UnescapeDataString(pathPart.Replace('\\', '/'));
        anchorId = fragmentIndex >= 0
            ? NormalizeAnchorId(trimmed[(fragmentIndex + 1)..])
            : null;

        return !string.IsNullOrWhiteSpace(targetDocumentRelativePath);
    }

    private static string? ResolveDocumentRelativePath(
        string currentDocumentRelativePath,
        string targetDocumentRelativePath)
    {
        var resolvedSegments = SplitAndNormalizeSegments(currentDocumentRelativePath);
        if (resolvedSegments.Count > 0)
        {
            resolvedSegments.RemoveAt(resolvedSegments.Count - 1);
        }

        foreach (var segment in SplitAndNormalizeSegments(targetDocumentRelativePath))
        {
            if (segment is "." or "")
            {
                continue;
            }

            if (segment == "..")
            {
                if (resolvedSegments.Count == 0)
                {
                    return null;
                }

                resolvedSegments.RemoveAt(resolvedSegments.Count - 1);
                continue;
            }

            resolvedSegments.Add(segment);
        }

        return resolvedSegments.Count == 0
            ? null
            : string.Join('/', resolvedSegments);
    }

    private static List<string> SplitAndNormalizeSegments(string relativePath)
    {
        return relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => Uri.UnescapeDataString(segment.Trim()))
            .Where(static segment => segment.Length > 0)
            .ToList();
    }

    private static string? NormalizeAnchorId(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var anchorId = Uri.UnescapeDataString(fragment.Trim());
        if (anchorId.StartsWith('#'))
        {
            anchorId = anchorId[1..];
        }

        return string.IsNullOrWhiteSpace(anchorId) ? null : anchorId;
    }
}
