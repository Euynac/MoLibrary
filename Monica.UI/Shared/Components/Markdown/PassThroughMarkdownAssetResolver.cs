namespace Monica.UI.Shared.Components.Markdown;

internal sealed class PassThroughMarkdownAssetResolver : IMoMarkdownAssetResolver
{
    public string? ResolveUrl(
        string? originalUrl,
        bool isImage,
        string? scopeKey,
        string? documentRelativePath,
        string? documentCulture)
    {
        return originalUrl;
    }
}
