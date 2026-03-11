namespace Monica.Markdown.UIMarkdown.Models;

/// <summary>
/// Selects the fuzzy matching strategy used by markdown document search.
/// </summary>
public enum EMarkdownDocumentSearchAlgorithm
{
    /// <summary>
    /// Splits the query into keywords and requires each keyword to match relevant fields.
    /// </summary>
    KeywordFuzzy = 0,

    /// <summary>
    /// Allows non-contiguous character subsequence matches for broader recall.
    /// </summary>
    LooseSubsequence = 1
}
