using Monica.Markdown.UIMarkdown.Models;
using Monica.UI.Shared.Components.Markdown;

namespace Monica.Markdown.UIMarkdown.Support;

/// <summary>
/// Rewrites local markdown resources to runtime-safe viewer or asset URLs.
/// </summary>
public class MarkdownAssetUrlResolver(
    MarkdownLocalAssetService assetService) : IMoMarkdownAssetResolver
{
    /// <summary>
    /// Resolves a markdown URL for rendering.
    /// </summary>
    public string? ResolveUrl(
        string? originalUrl,
        bool isImage,
        string? scopeKey,
        string? documentRelativePath,
        string? documentCulture)
    {
        if (!isImage)
        {
            return MarkdownViewerLocation.TryResolveDocumentLink(
                       scopeKey,
                       documentRelativePath,
                       documentCulture,
                       originalUrl)
                   ?? originalUrl;
        }

        return assetService.BuildRenderUrl(
            originalUrl,
            true,
            scopeKey,
            documentRelativePath);
    }
}
