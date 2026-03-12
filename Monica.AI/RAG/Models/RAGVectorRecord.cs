using System.Security.Cryptography;
using System.Text;
using Monica.AI.RAG.Abstractions;

namespace Monica.AI.RAG.Models;

/// <summary>
/// Strongly typed vector record persisted in the backing vector store.
/// </summary>
public sealed class RAGVectorRecord
{
    /// <summary>
    /// Storage-native key used by the underlying vector store.
    /// </summary>
    public required Guid StorageKey { get; init; }

    /// <summary>
    /// Stable logical key used by Monica for diagnostics and validation.
    /// </summary>
    public required string LogicalKey { get; init; }

    public required string KnowledgeBaseId { get; init; }

    public required string DocumentPath { get; init; }

    public required string DocumentTitle { get; init; }

    public required string Content { get; init; }

    public string? SectionPath { get; init; }

    public int ChunkIndex { get; init; }

    public int ChunkStart { get; init; }

    public int ChunkEnd { get; init; }

    public required string ChunkerId { get; init; }

    public required float[] ContentEmbedding { get; init; }

    public static RAGVectorRecord Create(
        string knowledgeBaseId,
        string documentPath,
        string documentTitle,
        DocumentChunk chunk,
        string chunkerId,
        float[] contentEmbedding)
    {
        var logicalKey = BuildLogicalKey(knowledgeBaseId, documentPath, chunk.ChunkIndex);

        return new RAGVectorRecord
        {
            StorageKey = BuildStorageKey(logicalKey),
            LogicalKey = logicalKey,
            KnowledgeBaseId = knowledgeBaseId,
            DocumentPath = documentPath,
            DocumentTitle = documentTitle,
            Content = chunk.Content,
            SectionPath = chunk.SectionPath,
            ChunkIndex = chunk.ChunkIndex,
            ChunkStart = chunk.StartOffset,
            ChunkEnd = chunk.EndOffset,
            ChunkerId = chunkerId,
            ContentEmbedding = contentEmbedding
        };
    }

    public static string BuildLogicalKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => $"{knowledgeBaseId}_{documentPath}_{chunkIndex}";

    public static Guid BuildStorageKey(string knowledgeBaseId, string documentPath, int chunkIndex)
        => BuildStorageKey(BuildLogicalKey(knowledgeBaseId, documentPath, chunkIndex));

    public static Guid BuildStorageKey(string logicalKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);

        var bytes = Encoding.UTF8.GetBytes(logicalKey);
        var hash = SHA256.HashData(bytes);
        return new Guid(hash.AsSpan(0, 16));
    }
}
