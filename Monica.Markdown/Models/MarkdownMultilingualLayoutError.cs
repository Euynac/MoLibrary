namespace Monica.Markdown.Models;

/// <summary>
/// Identifies invalid multilingual markdown layouts detected during scanning.
/// </summary>
public enum MarkdownMultilingualLayoutError
{
    /// <summary>
    /// Markdown files were detected outside supported language root folders
    /// after multilingual mode found at least one language root.
    /// </summary>
    RootMarkdownFilesOutsideLanguageFolders
}
