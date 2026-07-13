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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var entries = GetEntries(kind);
        return !entries.TryGetValue(name, out var isEnabled) || isEnabled;
    }

    /// <summary>
    /// Gets whether the owning capability catalog is globally enabled.
    /// </summary>
    public bool IsCatalogEnabled(AgentCapabilityKind kind)
    {
        return kind switch
        {
            AgentCapabilityKind.Skill => SkillsEnabled,
            AgentCapabilityKind.Mcp => McpEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    /// <summary>
    /// Changes the global runtime switch for one capability catalog.
    /// </summary>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool SetCatalogEnabled(AgentCapabilityKind kind, bool isEnabled)
    {
        if (IsCatalogEnabled(kind) == isEnabled)
        {
            return false;
        }

        switch (kind)
        {
            case AgentCapabilityKind.Skill:
                SkillsEnabled = isEnabled;
                break;
            case AgentCapabilityKind.Mcp:
                McpEnabled = isEnabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return true;
    }

    /// <summary>
    /// Changes the runtime switch for one capability entry.
    /// </summary>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool SetEntryEnabled(AgentCapabilityKind kind, string name, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        var entries = GetEntries(kind);
        if (entries.TryGetValue(normalizedName, out var current) && current == isEnabled)
        {
            return false;
        }

        entries[normalizedName] = isEnabled;
        return true;
    }

    /// <summary>
    /// Removes the persisted runtime override for one capability entry.
    /// </summary>
    /// <returns><see langword="true"/> when an override was removed.</returns>
    public bool RemoveEntryOverride(AgentCapabilityKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetEntries(kind).Remove(name.Trim());
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

    /// <summary>
    /// Changes whether one skill is exposed through the MCP server on the next host startup.
    /// </summary>
    /// <returns><see langword="true"/> when the state changed.</returns>
    public bool SetSkillMcpServerEnabled(string skillName, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        var normalizedName = skillName.Trim();
        if (SkillMcpServers.TryGetValue(normalizedName, out var current) && current == isEnabled)
        {
            return false;
        }

        SkillMcpServers[normalizedName] = isEnabled;
        return true;
    }

    private Dictionary<string, bool> GetEntries(AgentCapabilityKind kind)
    {
        return kind switch
        {
            AgentCapabilityKind.Skill => SkillEntries,
            AgentCapabilityKind.Mcp => McpEntries,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
