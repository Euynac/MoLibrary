namespace Monica.AI.UI.UIKnowledgeBase.Components;

/// <summary>
/// Carries knowledge base values submitted from the create/edit dialog.
/// </summary>
public sealed record KnowledgeBaseDialogResult
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
}
