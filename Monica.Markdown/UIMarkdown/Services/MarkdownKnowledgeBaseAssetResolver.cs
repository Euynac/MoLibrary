using Monica.UI.Components.Markdown;

namespace Monica.Markdown.UIMarkdown.Services;

/// <summary>
/// Rewrites local markdown image references to the module asset endpoint.
/// </summary>
public class MarkdownKnowledgeBaseAssetResolver(
    MarkdownLocalImageAssetService assetService) : IMoMarkdownAssetResolver
{
    /// <summary>
    /// Resolves a markdown URL for rendering.
    /// </summary>
    public string? ResolveUrl(
        string? originalUrl,
        bool isImage,
        string? scopeKey,
        string? documentRelativePath)
    {
        return assetService.BuildRenderUrl(
            originalUrl,
            isImage,
            scopeKey,
            documentRelativePath);
    }
}
