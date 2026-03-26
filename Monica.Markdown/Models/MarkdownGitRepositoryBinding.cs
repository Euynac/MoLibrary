namespace Monica.Markdown.Models;

/// <summary>
/// Binds a Git repository to a Markdown document group for refresh integration.
/// </summary>
public class MarkdownGitRepositoryBinding
{
    /// <summary>
    /// Gets the Git repository identifier.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Gets the Markdown document group key.
    /// </summary>
    public required string DocumentGroupKey { get; init; }
}
