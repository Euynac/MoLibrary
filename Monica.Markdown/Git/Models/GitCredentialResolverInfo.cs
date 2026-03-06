namespace Monica.Markdown.Git.Models;

/// <summary>
/// Describes a registered credential resolver.
/// </summary>
public sealed class GitCredentialResolverInfo
{
    /// <summary>
    /// Gets the resolver identifier.
    /// </summary>
    public required string ResolverId { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the execution order.
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// Gets the implementation type name.
    /// </summary>
    public required string ImplementationType { get; init; }
}
