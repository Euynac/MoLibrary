using System.Collections.Concurrent;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;

namespace Monica.Markdown.Providers.FileSystem;

/// <summary>
/// File-system-based change notifier using <see cref="FileSystemWatcher"/>.
/// Monitors directories for markdown file changes and raises events.
/// </summary>
public class FileSystemMarkdownChangeNotifier : IMarkdownChangeNotifier
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(
        StringComparer.OrdinalIgnoreCase);

    public event EventHandler<MarkdownDocumentsChangedEventArgs>? DocumentsChanged;

    public void StartWatching(string groupKey, string basePath)
    {
        if (_watchers.ContainsKey(groupKey))
            return;

        var absolutePath = Path.GetFullPath(basePath);
        if (!Directory.Exists(absolutePath))
            return;

        var watcher = new FileSystemWatcher(absolutePath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, e) => OnFileChanged(groupKey, e.FullPath, DocumentChangeType.Created);
        watcher.Changed += (_, e) => OnFileChanged(groupKey, e.FullPath, DocumentChangeType.Modified);
        watcher.Deleted += (_, e) => OnFileChanged(groupKey, e.FullPath, DocumentChangeType.Deleted);
        watcher.Renamed += (_, e) => OnFileChanged(groupKey, e.FullPath, DocumentChangeType.Renamed);

        _watchers.TryAdd(groupKey, watcher);
    }

    public void StopWatching(string groupKey)
    {
        if (_watchers.TryRemove(groupKey, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _watchers)
        {
            kvp.Value.EnableRaisingEvents = false;
            kvp.Value.Dispose();
        }

        _watchers.Clear();
    }

    private void OnFileChanged(
        string groupKey, string filePath, DocumentChangeType changeType)
    {
        var extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
            return;

        DocumentsChanged?.Invoke(this, new MarkdownDocumentsChangedEventArgs(
            groupKey, [filePath], changeType));
    }
}
