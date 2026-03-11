namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Describes a markdown document search request initiated from the UI.
/// </summary>
public sealed record MarkdownDocumentSearchRequest(
    string Query,
    string? CurrentGroupKey = null,
    bool IncludeAllKnowledgeBases = false);
