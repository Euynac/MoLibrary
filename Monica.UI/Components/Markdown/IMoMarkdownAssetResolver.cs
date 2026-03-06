namespace Monica.UI.Components.Markdown;

/// <summary>
/// Resolves markdown URLs to runtime-safe locations.
/// </summary>
public interface IMoMarkdownAssetResolver
{
    /// <summary>
    /// Resolves a markdown URL for rendering.
    /// </summary>
    /// <param name="originalUrl">The original markdown URL.</param>
    /// <param name="isImage">Whether the URL belongs to an image node.</param>
    /// <param name="scopeKey">An optional logical scope key for the current markdown document.</param>
    /// <param name="documentRelativePath">The current markdown document path relative to its logical scope.</param>
    /// <returns>The URL to render.</returns>
    string? ResolveUrl(string? originalUrl, bool isImage, string? scopeKey, string? documentRelativePath);
}
