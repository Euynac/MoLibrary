using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;

namespace Monica.Markdown.Services;

/// <summary>
/// Default title provider that uses the file name without extension as the document title.
/// </summary>
public class FileNameDocumentTitleProvider : IDocumentTitleProvider
{
    public string ResolveTitle(string filePath, MarkdownFrontMatter? frontMatter)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }
}
