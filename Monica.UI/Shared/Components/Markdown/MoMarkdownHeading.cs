namespace Monica.UI.Shared.Components.Markdown;

/// <summary>
/// Represents a markdown heading that can be linked by URL fragment.
/// </summary>
public sealed record MoMarkdownHeading(
    string Id,
    string Title,
    int Level);
