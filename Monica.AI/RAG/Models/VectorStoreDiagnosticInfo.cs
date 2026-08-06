namespace Monica.AI.RAG.Models;

/// <summary>
/// Runtime diagnostic metadata for the configured RAG vector store.
/// </summary>
public sealed record VectorStoreDiagnosticInfo
{
    /// <summary>
    /// Stable provider kind captured during vector-store registration.
    /// </summary>
    public required string ProviderKind { get; init; }

    /// <summary>
    /// User-facing provider display name.
    /// </summary>
    public required string ProviderDisplayName { get; init; }

    /// <summary>
    /// Runtime implementation type registered as <c>VectorStore</c>.
    /// </summary>
    public required string RuntimeTypeName { get; init; }

    /// <summary>
    /// VectorData metadata system name, when provided by the vector-store implementation.
    /// </summary>
    public string? VectorStoreSystemName { get; init; }

    /// <summary>
    /// VectorData metadata store/database name, when provided by the vector-store implementation.
    /// </summary>
    public string? VectorStoreName { get; init; }

    /// <summary>
    /// Collection prefix configured for RAG knowledge-base collections.
    /// </summary>
    public required string CollectionNamePrefix { get; init; }

    /// <summary>
    /// Display-safe configuration entries captured from module registration.
    /// </summary>
    public IReadOnlyList<VectorStoreConfigurationEntry> ConfigurationEntries { get; init; } = [];
}
