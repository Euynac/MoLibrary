namespace Monica.AI.RAG.Models;

/// <summary>
/// Describes how a text search score should be interpreted by diagnostics and UI.
/// </summary>
public enum TextSearchScoreKind
{
    /// <summary>
    /// The score is a vector similarity-style value that can be shown as a percentage.
    /// </summary>
    VectorSimilarity = 0,

    /// <summary>
    /// The score is a hybrid ranking value and should not be presented as a true similarity percentage.
    /// </summary>
    HybridRank = 1
}
