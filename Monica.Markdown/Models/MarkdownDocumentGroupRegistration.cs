namespace Monica.Markdown.Models;

/// <summary>
/// Registration-time descriptor for a markdown document group.
/// Captured during Guide configuration and used to scan documents at runtime.
/// </summary>
public class MarkdownDocumentGroupRegistration
{
    /// <summary>
    /// Unique identifier for this document group.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display title for this document group.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Folder path (absolute or relative to working directory) containing markdown files.
    /// </summary>
    public required string BasePath { get; init; }

    /// <summary>
    /// Optional description of this document group.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Additional folder names to exclude from scanning for this specific document group.
    /// These are combined with global exclusions from ModuleMarkdownOption.
    /// Matching is case-insensitive and applies to directory names at any level.
    /// </summary>
    public string[]? ExcludedFolders { get; init; }
}
