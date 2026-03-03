namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Strategy for splitting a document into chunks for embedding.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Stable identifier for this chunker.
    /// </summary>
    string ChunkerId { get; }

    /// <summary>
    /// Display name used by management UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Optional description for administrators.
    /// </summary>
    string? Description { get; }

    IReadOnlyList<string> SupportedExtensions { get; }

    IReadOnlyList<DocumentChunk> ChunkDocument(
        string content, string documentPath, string documentTitle);
}

public record DocumentChunk(
    string Content,
    string? SectionPath,
    int ChunkIndex,
    int StartOffset,
    int EndOffset);
