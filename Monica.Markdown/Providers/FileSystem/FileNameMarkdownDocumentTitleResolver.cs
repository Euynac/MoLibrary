using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.Markdown.Providers.FileSystem;

/// <summary>
/// Default title provider that uses the file name without extension as the document title.
/// </summary>
public class FileNameMarkdownDocumentTitleResolver : IMarkdownDocumentTitleResolver
{
    public string ResolveTitle(string filePath, MarkdownFrontMatter? frontMatter)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }
}
