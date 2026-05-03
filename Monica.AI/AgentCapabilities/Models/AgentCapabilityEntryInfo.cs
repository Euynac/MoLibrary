using Monica.AI.Mcp.Models;

namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Management metadata for one skill, MCP server, or external MCP client.
/// </summary>
public sealed record AgentCapabilityEntryInfo
{
    /// <summary>
    /// Creates capability metadata for management and slash-command UI.
    /// </summary>
    public AgentCapabilityEntryInfo(
        AgentCapabilityKind kind,
        string name,
        string title,
        string description,
        string? content,
        string? implementationType,
        IReadOnlyList<string> requiredModules,
        bool isBuiltInEnabled,
        bool isCatalogEnabled,
        bool isEntryEnabled,
        string? disabledReason,
        IReadOnlyList<AgentCapabilityToolInfo> tools,
        IReadOnlyList<AgentCapabilityResourceInfo> resources,
        McpCatalogSourceKind? mcpSourceKind = null,
        McpServerTransportKind? mcpTransportKind = null,
        string? mcpEndpointPath = null,
        string? mcpDisplayUrl = null,
        ExternalMcpClientProfile? mcpExternalProfile = null,
        string? skillMcpServerName = null,
        bool canExposeAsMcpServer = false,
        bool isMcpServerExposureEnabled = false,
        bool mcpServerExposureRequiresRestart = false,
        bool isUserManaged = false,
        string? discoveryError = null,
        AgentCapabilitySourceKind? sourceKind = null,
        string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(requiredModules);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(resources);

        Kind = kind;
        Name = name.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? Name : title.Trim();
        Description = description;
        Content = content;
        ImplementationType = implementationType;
        RequiredModules = requiredModules;
        IsBuiltInEnabled = isBuiltInEnabled;
        IsCatalogEnabled = isCatalogEnabled;
        IsEntryEnabled = isEntryEnabled;
        DisabledReason = disabledReason;
        Tools = tools;
        Resources = resources;
        McpSourceKind = mcpSourceKind;
        McpTransportKind = mcpTransportKind;
        McpEndpointPath = mcpEndpointPath;
        McpDisplayUrl = mcpDisplayUrl;
        McpExternalProfile = mcpExternalProfile;
        SkillMcpServerName = skillMcpServerName;
        CanExposeAsMcpServer = canExposeAsMcpServer;
        IsMcpServerExposureEnabled = isMcpServerExposureEnabled;
        McpServerExposureRequiresRestart = mcpServerExposureRequiresRestart;
        IsUserManaged = isUserManaged;
        DiscoveryError = discoveryError;
        SourceKind = sourceKind;
        SourcePath = sourcePath;
    }

    /// <summary>
    /// Catalog that owns this entry.
    /// </summary>
    public AgentCapabilityKind Kind { get; }

    /// <summary>
    /// Stable entry name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable title. Defaults to <see cref="Name"/> when no richer title is available.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Short discovery description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Full skill instructions or MCP initialization instructions.
    /// </summary>
    public string? Content { get; }

    /// <summary>
    /// Concrete .NET type for Monica-defined capabilities, or registration type for external entries.
    /// </summary>
    public string? ImplementationType { get; }

    /// <summary>
    /// Module keys required before the capability can be active.
    /// </summary>
    public IReadOnlyList<string> RequiredModules { get; }

    /// <summary>
    /// Whether the capability author enabled this entry in code.
    /// </summary>
    public bool IsBuiltInEnabled { get; }

    /// <summary>
    /// Whether the catalog-level runtime switch is enabled.
    /// </summary>
    public bool IsCatalogEnabled { get; }

    /// <summary>
    /// Whether this entry's runtime switch is enabled.
    /// </summary>
    public bool IsEntryEnabled { get; }

    /// <summary>
    /// Effective availability after code-level, module-level, catalog-level, and entry-level checks.
    /// </summary>
    public bool IsEnabled => IsBuiltInEnabled && IsCatalogEnabled && IsEntryEnabled && DisabledReason is null;

    /// <summary>
    /// Human-readable reason the entry cannot currently be used.
    /// </summary>
    public string? DisabledReason { get; }

    /// <summary>
    /// Tools or scripts exposed by this entry.
    /// </summary>
    public IReadOnlyList<AgentCapabilityToolInfo> Tools { get; }

    /// <summary>
    /// Skill resources exposed by this entry. MCP entries currently have no Monica-owned resources here.
    /// </summary>
    public IReadOnlyList<AgentCapabilityResourceInfo> Resources { get; }

    /// <summary>
    /// Optional source classification for the entry.
    /// </summary>
    public AgentCapabilitySourceKind? SourceKind { get; }

    /// <summary>
    /// Optional source path for file-backed or externally registered entries.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    /// MCP origin when <see cref="Kind"/> is <see cref="AgentCapabilityKind.Mcp"/>.
    /// </summary>
    public McpCatalogSourceKind? McpSourceKind { get; }

    /// <summary>
    /// Monica-owned MCP transport for local MCP servers.
    /// </summary>
    public McpServerTransportKind? McpTransportKind { get; }

    /// <summary>
    /// HTTP route pattern for local HTTP MCP servers.
    /// </summary>
    public string? McpEndpointPath { get; }

    /// <summary>
    /// Optional externally reachable MCP URL configured for management UIs.
    /// </summary>
    public string? McpDisplayUrl { get; }

    /// <summary>
    /// External HTTP MCP profile when this entry is backed by a remote MCP client.
    /// </summary>
    public ExternalMcpClientProfile? McpExternalProfile { get; }

    /// <summary>
    /// MCP server name generated from this skill when <see cref="CanExposeAsMcpServer" /> is enabled.
    /// </summary>
    public string? SkillMcpServerName { get; }

    /// <summary>
    /// Gets whether this skill can be exposed as an MCP server.
    /// </summary>
    public bool CanExposeAsMcpServer { get; }

    /// <summary>
    /// Gets whether this skill is configured to be exposed as an MCP server.
    /// </summary>
    public bool IsMcpServerExposureEnabled { get; }

    /// <summary>
    /// Gets whether changing MCP exposure requires a host restart before the MCP endpoint reflects it.
    /// </summary>
    public bool McpServerExposureRequiresRestart { get; }

    /// <summary>
    /// Gets whether this capability can be edited from the runtime management UI.
    /// </summary>
    public bool IsUserManaged { get; }

    /// <summary>
    /// Error captured while discovering capability tools, if discovery failed.
    /// </summary>
    public string? DiscoveryError { get; }

    /// <summary>
    /// Stable UI key used by management rows and slash chips.
    /// </summary>
    public string Key => AgentCapabilityReference.CreateKey(Kind, Name);

    /// <summary>
    /// Converts this entry into a slash-command reference candidate.
    /// </summary>
    public AgentCapabilityReferenceCandidate ToReferenceCandidate()
    {
        return new AgentCapabilityReferenceCandidate(
            Kind,
            Name,
            Description,
            IsEnabled,
            DisabledReason,
            Tools.Select(tool => tool.Name).ToList());
    }
}
