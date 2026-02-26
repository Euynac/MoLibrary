namespace Monica.AI.RAG.Models;

/// <summary>
/// Represents a chunk's position within the original document text.
/// Used for highlighting chunks in the chunk viewer dialog.
/// </summary>
public record ChunkHighlight
{
    /// <summary>
    /// Chunk index (0-based).
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Start position in the original text (character index).
    /// </summary>
    public required int Start { get; init; }

    /// <summary>
    /// End position in the original text (character index).
    /// </summary>
    public required int End { get; init; }

    /// <summary>
    /// Section or heading this chunk belongs to (optional).
    /// </summary>
    public string? Section { get; init; }

    /// <summary>
    /// Whether this chunk is the matched chunk in search results.
    /// Matched chunks are highlighted differently (green vs yellow).
    /// </summary>
    public bool IsMatched { get; init; }
}
