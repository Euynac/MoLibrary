using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Modules;
using Monica.Markdown.Scanner;

namespace Monica.Markdown.Services;

/// <summary>
/// File-system-based markdown document provider.
/// Delegates scanning to <see cref="DocumentScanner"/> and reads content from disk.
/// </summary>
public class FileMarkdownDocumentProvider(
    IDocumentTitleProvider titleProvider) : IMarkdownDocumentProvider
{
    public Task<MarkdownDocumentGroup> ScanGroupAsync(
        DocumentGroupRegistration registration,
        ModuleMarkdownOption options)
    {
        return DocumentScanner.ScanAsync(registration, options, titleProvider);
    }

    public async Task<string> GetDocumentContentAsync(string documentPath)
    {
        var absolutePath = Path.GetFullPath(documentPath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException(
                $"File not found: '{absolutePath}'.", absolutePath);

        return await File.ReadAllTextAsync(absolutePath);
    }

    public IMarkdownChangeNotifier? GetChangeNotifier()
    {
        return new FileSystemChangeNotifier();
    }
}
