using Microsoft.Extensions.Options;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;
using Monica.Modules;

namespace Monica.Markdown.Providers.FileSystem;

/// <summary>
/// File-system-based markdown document provider.
/// Delegates scanning to <see cref="FileSystemMarkdownScanner"/> and reads content from disk.
/// </summary>
public class FileSystemMarkdownDocumentProvider(
    IMarkdownDocumentTitleResolver titleProvider,
    IOptions<ModuleLocalizationOption> localizationOptions) : IMarkdownDocumentProvider
{
    public Task<MarkdownDocumentGroup> ScanGroupAsync(
        MarkdownDocumentGroupRegistration registration,
        ModuleMarkdownOption options)
    {
        return FileSystemMarkdownScanner.ScanAsync(
            registration,
            options,
            titleProvider,
            localizationOptions.Value);
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
        return new FileSystemMarkdownChangeNotifier();
    }
}
