namespace Monica.Markdown.Models;

/// <summary>
/// Data payload for tree nodes representing markdown documents or directories.
/// Used as <c>TreeNode&lt;MarkdownDocumentNodeData&gt;</c> from Monica.Tool.Algorithm.Tree.
/// </summary>
/// <param name="Name">Physical path segment of this node (file name or directory name).</param>
/// <param name="IsDocument">Whether this node represents a document (leaf) or directory (branch).</param>
/// <param name="Document">The associated markdown document, if this is a document node.</param>
/// <param name="DisplayName">Optional label used for navigation display.</param>
/// <param name="NavigationOrder">Optional explicit order used for sibling sorting.</param>
public record MarkdownDocumentNodeData(
    string Name,
    bool IsDocument,
    MarkdownDocument? Document = null,
    string? DisplayName = null,
    int? NavigationOrder = null)
{
    /// <summary>
    /// Gets the navigation label, falling back to the physical path segment.
    /// </summary>
    public string ResolvedDisplayName =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? Name
            : DisplayName;
}
