namespace Monica.AI.RAG.Models;

public record IndexingProgress(
    int ProcessedChunks,
    int TotalChunks,
    string CurrentDocument);
