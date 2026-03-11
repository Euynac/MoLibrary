namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Describes how the viewer should locate and highlight a matched text range after opening a document.
/// </summary>
public sealed record MarkdownSearchLocator(
    string? AnchorId,
    string MatchedText,
    string PrefixContext,
    string SuffixContext,
    int? HeadingLevel = null);
