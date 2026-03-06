namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Represents a validated local image asset that can be streamed to the browser.
/// </summary>
public sealed record MarkdownLocalImageAsset(string FilePath, string ContentType);
