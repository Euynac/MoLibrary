namespace Monica.Core.Skills.Models;

/// <summary>
/// Transport used when a Monica skill is exposed as an MCP server.
/// </summary>
public enum SkillMcpServerTransportKind
{
    /// <summary>
    /// Exposes the skill through Monica's HTTP MCP endpoint.
    /// </summary>
    Http,

    /// <summary>
    /// Exposes the skill through Monica's stdio MCP host.
    /// </summary>
    Stdio
}

/// <summary>
/// Describes how a Monica skill may be exposed as an MCP server.
/// </summary>
/// <remarks>
/// Skill authors return this definition from <see cref="Skill.McpServerDefinition" /> when the same
/// skill tools should also be available to external MCP clients. Runtime state can enable or disable the
/// exposure per skill; because MCP endpoints are built during startup, changes normally require a host restart.
/// </remarks>
public sealed record SkillMcpServerDefinition
{
    /// <summary>
    /// Creates an MCP exposure definition for a Monica skill.
    /// </summary>
    /// <param name="name">
    /// Stable MCP server identifier. Keep this lowercase and unique across Monica-defined MCP servers.
    /// </param>
    /// <param name="description">Short description shown to MCP clients and management UIs.</param>
    public SkillMcpServerDefinition(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Name = name.Trim();
        Description = description.Trim();
    }

    /// <summary>
    /// Stable MCP server identifier shown to MCP clients and management UIs.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Short description of the server's capabilities.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Optional implementation version reported to MCP clients.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Optional display title shown by MCP clients and management UIs.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional instructions sent to MCP clients during initialization.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Optional website URL for documentation or management UI links.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Transport used when the skill is exposed as an MCP server.
    /// </summary>
    public SkillMcpServerTransportKind TransportKind { get; init; } = SkillMcpServerTransportKind.Http;

    /// <summary>
    /// Whether new installations should expose this skill as an MCP server before a runtime override exists.
    /// </summary>
    public bool EnabledByDefault { get; init; }

    /// <summary>
    /// Whether the generated MCP tools should also be exposed through Monica's local AI agent MCP-tool path.
    /// </summary>
    /// <remarks>
    /// Most skills should leave this disabled because the Skill catalog already exposes their tools to Monica
    /// agents. Enable it only when the MCP representation should intentionally be reusable by local agents too.
    /// </remarks>
    public bool IsLocalToolEnabled { get; init; }
}
