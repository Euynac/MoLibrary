using Monica.Tool.Algorithm.Tree;

namespace Monica.Markdown.Models;

/// <summary>
/// Runtime representation of a scanned markdown document group.
/// Contains the tree structure and metadata about the group's documents.
/// </summary>
public class MarkdownDocumentGroup
{
    /// <summary>
    /// Unique identifier for this document group.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display title for this document group.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional description of this document group.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Resolved absolute base path of this document group.
    /// </summary>
    public required string BasePath { get; init; }

    /// <summary>
    /// Whether the base path exists and is accessible.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Total number of markdown files found in this group.
    /// </summary>
    public required int DocumentCount { get; init; }

    /// <summary>
    /// Tree root node containing the hierarchical document structure.
    /// </summary>
    public required TreeNode<MarkdownDocumentNodeData> RootNode { get; init; }
}
