namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Complete management view for skill and MCP capability catalogs.
/// </summary>
public sealed record AgentCapabilityManagementInfo(
    bool AreSkillsEnabled,
    bool AreMcpToolsEnabled,
    long Revision,
    IReadOnlyList<AgentCapabilityEntryInfo> Skills,
    IReadOnlyList<AgentCapabilityEntryInfo> McpEntries,
    IReadOnlyList<AgentCapabilityFileSkillSourceStatusInfo> FileSkillSources)
{
    /// <summary>
    /// All entries in display order.
    /// </summary>
    public IReadOnlyList<AgentCapabilityEntryInfo> Entries => [..Skills, ..McpEntries];
}
