namespace Monica.AI.Mcp.Models;

/// <summary>
/// Result of writing one generated MCP agent skill pack into an agent skills directory.
/// </summary>
public sealed record McpSkillPackExportResult
{
    /// <summary>
    /// Creates an export result.
    /// </summary>
    public McpSkillPackExportResult(McpSkillPack pack, string skillDirectoryPath, IReadOnlyList<string> writtenFiles)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectoryPath);
        ArgumentNullException.ThrowIfNull(writtenFiles);

        Pack = pack;
        SkillDirectoryPath = skillDirectoryPath;
        WrittenFiles = writtenFiles;
    }

    /// <summary>
    /// The rendered skill pack that was written.
    /// </summary>
    public McpSkillPack Pack { get; }

    /// <summary>
    /// Absolute path of the created skill directory below the requested target root.
    /// </summary>
    public string SkillDirectoryPath { get; }

    /// <summary>
    /// Absolute paths of the written files, in pack file order.
    /// </summary>
    public IReadOnlyList<string> WrittenFiles { get; }
}
