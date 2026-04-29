namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Status of a document in the indexing queue.
/// </summary>
public enum DocumentStatus
{
    /// <summary>
    /// Document is queued for indexing but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Document is currently being indexed.
    /// </summary>
    Indexing,

    /// <summary>
    /// Document has been successfully indexed.
    /// </summary>
    Done,

    /// <summary>
    /// Document indexing failed with an error.
    /// </summary>
    Error
}
