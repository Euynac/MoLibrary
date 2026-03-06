namespace Monica.UI.Components.Markdown;

internal sealed class PassThroughMarkdownAssetResolver : IMoMarkdownAssetResolver
{
    public string? ResolveUrl(string? originalUrl, bool isImage, string? scopeKey, string? documentRelativePath)
    {
        return originalUrl;
    }
}
