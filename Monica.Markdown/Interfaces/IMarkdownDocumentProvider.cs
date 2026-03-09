using Monica.Markdown.Models;
using Monica.Modules;

namespace Monica.Markdown.Interfaces;

/// <summary>
/// Abstraction for markdown document storage and retrieval.
/// Implementations provide access to documents from different backends
/// (file system, database, etc.).
/// </summary>
public interface IMarkdownDocumentProvider
{
    /// <summary>
    /// Scans and returns all documents for a registered group.
    /// </summary>
    Task<MarkdownDocumentGroup> ScanGroupAsync(
        DocumentGroupRegistration registration,
        ModuleMarkdownOption options);

    /// <summary>
    /// Reads the raw markdown content of a document.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the document cannot be found.
    /// </exception>
    Task<string> GetDocumentContentAsync(string documentPath);

    /// <summary>
    /// Gets the change notifier for this provider, if supported.
    /// Returns null if the provider does not support change detection.
    /// </summary>
    IMarkdownChangeNotifier? GetChangeNotifier();
}
