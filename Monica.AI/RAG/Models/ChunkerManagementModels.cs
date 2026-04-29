using Monica.AI.KnowledgeBase.Models;

namespace Monica.AI.RAG.Models;

/// <summary>
/// Chunker metadata for management UI.
/// </summary>
public sealed class ChunkerDescriptor
{
    public required string ChunkerId { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required IReadOnlyList<string> SupportedExtensions { get; init; }
}

/// <summary>
/// Routing status for one extension.
/// </summary>
public sealed class ChunkerRouteDescriptor
{
    public required string Extension { get; init; }

    public required string DefaultChunkerId { get; init; }

    public required IReadOnlyList<string> CandidateChunkerIds { get; init; }
}

/// <summary>
/// Aggregated chunker dashboard data.
/// </summary>
public sealed class ChunkerManagementState
{
    public required IReadOnlyList<ChunkerDescriptor> Chunkers { get; init; }

    public required IReadOnlyList<ChunkerRouteDescriptor> Routes { get; init; }
}

/// <summary>
/// Impact preview for changing default chunker of an extension.
/// </summary>
public sealed class ChunkerRoutingChangePreview
{
    public required string Extension { get; init; }

    public required string CurrentChunkerId { get; init; }

    public required string TargetChunkerId { get; init; }

    public required int AffectedDocumentCount { get; init; }

    public required IReadOnlyList<string> AffectedKnowledgeBaseIds { get; init; }
}

/// <summary>
/// Result after applying a routing change.
/// </summary>
public sealed class ChunkerRoutingApplyResult
{
    public required string Extension { get; init; }

    public required string TargetChunkerId { get; init; }

    public required int MarkedPendingDocumentCount { get; init; }
}

/// <summary>
/// Result of chunk test execution.
/// </summary>
public sealed class ChunkerTestResult
{
    public required string ChunkerId { get; init; }

    public required string DocumentName { get; init; }

    public required string OriginalText { get; init; }

    public required IReadOnlyList<ChunkHighlight> Chunks { get; init; }
}

/// <summary>
/// Chunk viewer payload.
/// </summary>
public sealed class DocumentChunkView
{
    public required string DocumentPath { get; init; }

    public required string DocumentName { get; init; }

    public required string OriginalText { get; init; }

    public required IReadOnlyList<ChunkHighlight> Chunks { get; init; }

    public required string ChunkerId { get; init; }

    public required bool IsPreview { get; init; }

    public string SourceKind { get; init; } = KnowledgeDocumentSourceKinds.Unknown;

    public string? SourceGroupKey { get; init; }
}

/// <summary>
/// Result of validating that persisted index metadata still has matching vectors in the active store.
/// </summary>
public sealed class KnowledgeBaseVectorValidationResult
{
    public required string KnowledgeBaseId { get; init; }

    public required bool HasPersistedIndexMetadata { get; init; }

    public required bool WasValidated { get; init; }

    public required bool HasValidVectors { get; init; }

    public required int IndexedDocumentCount { get; init; }

    public required int IndexedChunkCount { get; init; }

    public string? MissingDocumentPath { get; init; }

    public IReadOnlyList<string> MissingVectorRecordKeys { get; init; } = [];
}
