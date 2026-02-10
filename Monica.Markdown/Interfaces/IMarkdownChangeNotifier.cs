using Monica.Markdown.Models;

namespace Monica.Markdown.Interfaces;

/// <summary>
/// Notifies subscribers when documents in a group change.
/// Each provider implements its own detection mechanism.
/// </summary>
public interface IMarkdownChangeNotifier : IDisposable
{
    /// <summary>
    /// Raised when documents in a watched group change.
    /// </summary>
    event EventHandler<DocumentsChangedEventArgs> DocumentsChanged;

    /// <summary>
    /// Starts watching a document group for changes.
    /// </summary>
    /// <param name="groupKey">Unique key identifying the document group.</param>
    /// <param name="basePath">Base directory path to watch.</param>
    void StartWatching(string groupKey, string basePath);

    /// <summary>
    /// Stops watching a document group.
    /// </summary>
    /// <param name="groupKey">Unique key identifying the document group.</param>
    void StopWatching(string groupKey);
}
