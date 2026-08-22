namespace Monica.AI.Mcp.Models;

/// <summary>
/// One deterministic file inside a generated MCP agent skill pack.
/// </summary>
public sealed record McpSkillPackFile
{
    /// <summary>
    /// Creates a skill pack file.
    /// </summary>
    /// <param name="relativePath">Slash-separated path relative to the skill directory, for example <c>scripts/mcp-call.sh</c>.</param>
    /// <param name="content">File content with LF line endings.</param>
    public McpSkillPackFile(string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.Contains('\\')
            || relativePath.StartsWith('/')
            || relativePath.Split('/').Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                $"Skill pack file path '{relativePath}' must be a normalized relative slash path.",
                nameof(relativePath));
        }

        ArgumentNullException.ThrowIfNull(content);

        RelativePath = relativePath;
        Content = content;
    }

    /// <summary>
    /// Slash-separated path relative to the skill directory, for example <c>scripts/mcp-call.sh</c>.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// File content with LF line endings.
    /// </summary>
    public string Content { get; }
}
