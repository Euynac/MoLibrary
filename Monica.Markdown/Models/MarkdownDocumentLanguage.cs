namespace Monica.Markdown.Models;

/// <summary>
/// Describes a detected document language inside a multilingual markdown group.
/// </summary>
public sealed record MarkdownDocumentLanguage(
    string Culture,
    string DisplayName,
    int DocumentCount);
