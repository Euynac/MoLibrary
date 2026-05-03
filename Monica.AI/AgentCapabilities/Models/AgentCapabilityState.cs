namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Persisted runtime enablement state for agent capabilities.
/// </summary>
public sealed class AgentCapabilityState
{
    /// <summary>
    /// Revision incremented on every persisted change. Chat sessions use it to detect stale agent pipelines.
    /// </summary>
    public long Revision { get; set; } = 1;

    /// <summary>
    /// Global runtime switch for Monica skills.
    /// </summary>
    public bool SkillsEnabled { get; set; } = true;

    /// <summary>
    /// Global runtime switch for MCP tools exposed to Monica agents.
    /// </summary>
    public bool McpEnabled { get; set; } = true;

    /// <summary>
    /// Per-skill runtime enablement overrides keyed by skill name.
    /// </summary>
    public Dictionary<string, bool> SkillEntries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-MCP-entry runtime enablement overrides keyed by server or client name.
    /// </summary>
    public Dictionary<string, bool> McpEntries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-skill MCP exposure overrides keyed by skill name. Missing entries use the skill author's default.
    /// </summary>
    public Dictionary<string, bool> SkillMcpServers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the persisted entry state for the given capability. Missing entries default to enabled.
    /// </summary>
    public bool IsEntryEnabled(AgentCapabilityKind kind, string name)
    {
        var entries = kind == AgentCapabilityKind.Skill ? SkillEntries : McpEntries;
        return !entries.TryGetValue(name, out var isEnabled) || isEnabled;
    }

    /// <summary>
    /// Gets whether the owning capability catalog is globally enabled.
    /// </summary>
    public bool IsCatalogEnabled(AgentCapabilityKind kind)
    {
        return kind == AgentCapabilityKind.Skill ? SkillsEnabled : McpEnabled;
    }

    /// <summary>
    /// Gets whether a skill should be exposed as an MCP server after applying runtime overrides.
    /// </summary>
    /// <param name="skillName">Skill name from the skill definition.</param>
    /// <param name="enabledByDefault">Author-defined exposure default used when no runtime override exists.</param>
    public bool IsSkillMcpServerEnabled(string skillName, bool enabledByDefault)
    {
        return SkillMcpServers.TryGetValue(skillName, out var isEnabled)
            ? isEnabled
            : enabledByDefault;
    }
}
