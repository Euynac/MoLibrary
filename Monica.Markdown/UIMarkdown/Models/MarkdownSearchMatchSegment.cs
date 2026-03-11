namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Represents a highlighted match segment inside a preview text.
/// </summary>
public sealed record MarkdownSearchMatchSegment(int Start, int Length);
