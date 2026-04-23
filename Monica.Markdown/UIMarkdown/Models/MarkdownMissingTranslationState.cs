namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Represents a requested document that does not exist in the currently
/// selected document language.
/// </summary>
public sealed record MarkdownMissingTranslationState(
    string DocumentRelativePath,
    string TargetCulture);
