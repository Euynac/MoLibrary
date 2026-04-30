namespace Monica.AI.KnowledgeBase.Models;

/// <summary>
/// Source content preview for one knowledge-base document.
/// </summary>
public sealed record KnowledgeBaseDocumentPreview
{
    /// <summary>
    /// Knowledge base identifier.
    /// </summary>
    public required string KnowledgeBaseId { get; init; }

    /// <summary>
    /// Document identifier in the knowledge-base inventory.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Display name of the document.
    /// </summary>
    public required string DocumentName { get; init; }

    /// <summary>
    /// Raw source content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Source kind used to resolve the document.
    /// </summary>
    public string SourceKind { get; init; } = KnowledgeDocumentSourceKinds.Unknown;

    /// <summary>
    /// Source group key when the source comes from a grouped catalog.
    /// </summary>
    public string? SourceGroupKey { get; init; }
}
