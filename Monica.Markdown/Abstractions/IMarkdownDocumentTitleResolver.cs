using Monica.Markdown.Models;

namespace Monica.Markdown.Abstractions;

/// <summary>
/// Provides title resolution for markdown documents.
/// Default implementation uses the file name without extension.
/// </summary>
public interface IMarkdownDocumentTitleResolver
{
    /// <summary>
    /// Resolves the display title for a markdown document.
    /// </summary>
    /// <param name="filePath">Absolute path to the markdown file.</param>
    /// <param name="frontMatter">Parsed front matter, if available.</param>
    /// <returns>The resolved display title.</returns>
    string ResolveTitle(string filePath, MarkdownFrontMatter? frontMatter);
}
