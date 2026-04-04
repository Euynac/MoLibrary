namespace Monica.Markdown.Models;

/// <summary>
/// Event arguments for document change notifications.
/// </summary>
public record MarkdownDocumentsChangedEventArgs(
    string GroupKey,
    IReadOnlyList<string> ChangedPaths,
    DocumentChangeType ChangeType);

/// <summary>
/// Type of document change detected.
/// </summary>
public enum DocumentChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}
