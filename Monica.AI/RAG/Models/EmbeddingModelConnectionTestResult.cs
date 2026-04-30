namespace Monica.AI.RAG.Models;

/// <summary>
/// Result of a live embedding model connection test.
/// </summary>
public sealed record EmbeddingModelConnectionTestResult
{
    /// <summary>
    /// Provider identifier used by the test.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Embedding model name used by the test.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Whether the embedding generation call succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Measured vector dimensions when the test succeeds.
    /// </summary>
    public int? Dimensions { get; init; }

    /// <summary>
    /// Human-readable success or failure details.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// UTC time when the test completed.
    /// </summary>
    public DateTimeOffset TestedAt { get; init; } = DateTimeOffset.UtcNow;
}
