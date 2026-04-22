using Monica.Markdown.Models;
using Monica.Tool.Algorithms.Trees;
using MudBlazor;

namespace Monica.Markdown.UIMarkdown.Support;

/// <summary>
/// Builds filtered tree view items for the markdown document navigation panel.
/// </summary>
public static class DocumentTreeItemBuilder
{
    /// <summary>
    /// Replaces the expanded path set with every directory path from the supplied tree.
    /// </summary>
    public static void ResetExpandedPaths(
        HashSet<string> expandedPaths,
        TreeNode<MarkdownDocumentNodeData>? node)
    {
        ArgumentNullException.ThrowIfNull(expandedPaths);

        expandedPaths.Clear();

        if (node is null)
        {
            return;
        }

        RegisterExpandedPaths(expandedPaths, node);
    }

    /// <summary>
    /// Builds tree items for rendering, optionally filtering by search text.
    /// </summary>
    public static List<DocumentTreeItem>? BuildTreeItems(
        TreeNode<MarkdownDocumentNodeData>? node,
        string? searchText,
        HashSet<string> expandedPaths,
        string currentPath = "",
        bool includeAllDescendants = false)
    {
        ArgumentNullException.ThrowIfNull(expandedPaths);

        if (node is null)
        {
            return null;
        }

        var items = new List<DocumentTreeItem>();
        foreach (var child in node.Children)
        {
            var childPath = string.IsNullOrWhiteSpace(currentPath)
                ? child.Data.Name
                : $"{currentPath}/{child.Data.Name}";
            var isMatched = includeAllDescendants || MatchesSearch(child, childPath, searchText);
            var shouldIncludeSubtree = includeAllDescendants || (!child.Data.IsDocument && isMatched);
            var childItems = BuildTreeItems(child, searchText, expandedPaths, childPath, shouldIncludeSubtree);

            if (!isMatched && childItems is null)
            {
                continue;
            }

            var shouldExpand = !string.IsNullOrWhiteSpace(searchText)
                ? !child.Data.IsDocument && (isMatched || childItems is not null)
                : expandedPaths.Contains(childPath);

            items.Add(new DocumentTreeItem(
                child.Data,
                child.Data.ResolvedDisplayName,
                childPath,
                childItems,
                shouldExpand));
        }

        return items.Count > 0 ? items : null;
    }

    /// <summary>
    /// Resolves the tree node that corresponds to the selected document.
    /// </summary>
    public static MarkdownDocumentNodeData? FindSelectedValue(
        TreeNode<MarkdownDocumentNodeData>? node,
        MarkdownDocument? selectedDocument)
    {
        if (node is null || selectedDocument is null)
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            if (child.Data.Document is not null
                && string.Equals(
                    child.Data.Document.RelativePath,
                    selectedDocument.RelativePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return child.Data;
            }

            var nested = FindSelectedValue(child, selectedDocument);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void RegisterExpandedPaths(
        HashSet<string> expandedPaths,
        TreeNode<MarkdownDocumentNodeData> node,
        string currentPath = "")
    {
        foreach (var child in node.Children)
        {
            var childPath = string.IsNullOrWhiteSpace(currentPath)
                ? child.Data.Name
                : $"{currentPath}/{child.Data.Name}";

            if (!child.Data.IsDocument)
            {
                expandedPaths.Add(childPath);
                RegisterExpandedPaths(expandedPaths, child, childPath);
            }
        }
    }

    private static bool MatchesSearch(
        TreeNode<MarkdownDocumentNodeData> node,
        string currentPath,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        if (node.Data.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node.Data.ResolvedDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (currentPath.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return node.Data.Document is not null
               && (node.Data.Document.NavigationTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                   || node.Data.Document.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                   || node.Data.Document.RelativePath.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tree item implementation consumed by MudBlazor tree view rendering.
    /// </summary>
    public sealed class DocumentTreeItem : ITreeItemData<MarkdownDocumentNodeData>
    {
        public DocumentTreeItem(
            MarkdownDocumentNodeData value,
            string text,
            string path,
            List<DocumentTreeItem>? children,
            bool expanded)
        {
            Value = value;
            Text = text;
            Path = path;
            Children = children;
            Icon = string.Empty;
            Expanded = expanded;
            Expandable = children?.Count > 0;
            Selected = false;
            Visible = true;
        }

        public MarkdownDocumentNodeData Value { get; }
        public string Path { get; }
        public string? Text { get; set; }
        public string? Icon { get; set; }
        public bool Expanded { get; set; }
        public bool Expandable { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
        public IReadOnlyCollection<ITreeItemData<MarkdownDocumentNodeData>>? Children { get; set; }
        public bool HasChildren => Children?.Count > 0;
    }
}
