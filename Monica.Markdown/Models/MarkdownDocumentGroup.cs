using Monica.Tool.Algorithms.Trees;

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
    /// Whether this group exposes multilingual document roots.
    /// </summary>
    public bool IsMultilingual { get; init; }

    /// <summary>
    /// Languages detected for this group when multilingual mode is active.
    /// </summary>
    public IReadOnlyList<MarkdownDocumentLanguage> Languages { get; init; } = [];

    /// <summary>
    /// Layout validation error detected for a multilingual group, if any.
    /// </summary>
    public MarkdownMultilingualLayoutError? MultilingualLayoutError { get; init; }

    /// <summary>
    /// Relative markdown paths that triggered <see cref="MultilingualLayoutError"/>.
    /// These values help users migrate invalid document layouts.
    /// </summary>
    public IReadOnlyList<string> MultilingualLayoutErrorPaths { get; init; } = [];

    /// <summary>
    /// Tree root node containing the hierarchical document structure.
    /// </summary>
    public required TreeNode<MarkdownDocumentNodeData> RootNode { get; init; }

    /// <summary>
    /// Flat document list for the entire group.
    /// </summary>
    public IReadOnlyList<MarkdownDocument> Documents { get; init; } = [];

    /// <summary>
    /// Culture-specific tree roots used by the built-in markdown viewer.
    /// </summary>
    public IReadOnlyDictionary<string, TreeNode<MarkdownDocumentNodeData>> LanguageRootNodes { get; init; } =
        new Dictionary<string, TreeNode<MarkdownDocumentNodeData>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Culture-specific document lists used by the built-in markdown viewer.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<MarkdownDocument>> LanguageDocuments { get; init; } =
        new Dictionary<string, IReadOnlyList<MarkdownDocument>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the group has a scan or layout error that prevents normal browsing.
    /// </summary>
    public bool HasConfigurationError => !IsValid || MultilingualLayoutError is not null;

    /// <summary>
    /// Resolves the tree visible for the supplied culture.
    /// </summary>
    public TreeNode<MarkdownDocumentNodeData> GetDocumentTree(string? culture = null)
    {
        if (!IsMultilingual || string.IsNullOrWhiteSpace(culture))
        {
            return RootNode;
        }

        var resolvedLanguage = ResolveLanguage(culture) ?? Languages.FirstOrDefault();
        if (resolvedLanguage is not null
            && LanguageRootNodes.TryGetValue(resolvedLanguage.Culture, out var tree))
        {
            return tree;
        }

        return RootNode;
    }

    /// <summary>
    /// Resolves the document list visible for the supplied culture.
    /// </summary>
    public IReadOnlyList<MarkdownDocument> GetDocuments(string? culture = null)
    {
        if (!IsMultilingual || string.IsNullOrWhiteSpace(culture))
        {
            return Documents;
        }

        var resolvedLanguage = ResolveLanguage(culture) ?? Languages.FirstOrDefault();
        if (resolvedLanguage is not null
            && LanguageDocuments.TryGetValue(resolvedLanguage.Culture, out var documents))
        {
            return documents;
        }

        return [];
    }

    /// <summary>
    /// Resolves the detected language that matches the supplied culture.
    /// </summary>
    public MarkdownDocumentLanguage? ResolveLanguage(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        return Languages.FirstOrDefault(language =>
            string.Equals(language.Culture, culture, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the best available language by walking preferred cultures in order.
    /// </summary>
    public string? ResolvePreferredLanguage(params string?[] preferredCultures)
    {
        foreach (var preferredCulture in preferredCultures)
        {
            var matchedLanguage = ResolveLanguage(preferredCulture);
            if (matchedLanguage is not null)
            {
                return matchedLanguage.Culture;
            }
        }

        return Languages.FirstOrDefault()?.Culture;
    }

    /// <summary>
    /// Finds a document by its viewer-relative path within the supplied culture.
    /// </summary>
    public MarkdownDocument? FindDocument(string? navigationRelativePath, string? culture = null)
    {
        if (string.IsNullOrWhiteSpace(navigationRelativePath))
        {
            return null;
        }

        var targetDocuments = GetDocuments(culture);
        return targetDocuments.FirstOrDefault(document =>
            string.Equals(
                document.NavigationRelativePath,
                navigationRelativePath,
                StringComparison.OrdinalIgnoreCase));
    }
}
