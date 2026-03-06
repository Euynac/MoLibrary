namespace Monica.Markdown.Git.Models;

/// <summary>
/// Binds a repository to a Markdown document group.
/// </summary>
public class GitRepositoryBinding
{
    /// <summary>
    /// Gets the repository identifier.
    /// </summary>
    public required string RepositoryId { get; init; }

    /// <summary>
    /// Gets the Markdown document group key.
    /// </summary>
    public required string DocumentGroupKey { get; init; }
}
