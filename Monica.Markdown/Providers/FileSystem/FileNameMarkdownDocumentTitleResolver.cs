using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.Markdown.Providers.FileSystem;

/// <summary>
/// Default title provider that prefers front matter <c>title</c> and falls back
/// to the file name without extension.
/// </summary>
public class FileNameMarkdownDocumentTitleResolver : IMarkdownDocumentTitleResolver
{
    public string ResolveTitle(string filePath, MarkdownFrontMatter? frontMatter)
    {
        if (!string.IsNullOrWhiteSpace(frontMatter?.Title))
        {
            return frontMatter.Title;
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }
}
