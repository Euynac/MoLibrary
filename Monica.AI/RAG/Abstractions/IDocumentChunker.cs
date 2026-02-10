namespace Monica.AI.RAG.Abstractions;

/// <summary>
/// Strategy for splitting a document into chunks for embedding.
/// </summary>
public interface IDocumentChunker
{
    IReadOnlyList<string> SupportedExtensions { get; }

    IReadOnlyList<DocumentChunk> ChunkDocument(
        string content, string documentPath, string documentTitle);
}

public record DocumentChunk(
    string Content,
    string? SectionPath,
    int ChunkIndex);
