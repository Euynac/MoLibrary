namespace Monica.AI.RAG.Models;

/// <summary>
/// Overrides the embedding model used to generate the query vector for search.
/// This does not mutate the knowledge base binding or stored collection schema.
/// </summary>
public sealed record RAGSearchEmbeddingOverride(
    string ProviderId,
    string ModelName);
