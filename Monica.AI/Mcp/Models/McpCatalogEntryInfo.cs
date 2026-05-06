namespace Monica.AI.Mcp.Models;

/// <summary>
/// Describes one local server or external client registered in Monica's MCP catalog.
/// </summary>
public sealed record McpCatalogEntryInfo
{
    /// <summary>
    /// Creates an MCP catalog entry description.
    /// </summary>
    public McpCatalogEntryInfo(
        string name,
        string description,
        McpCatalogSourceKind sourceKind,
        McpServerTransportKind? transportKind,
        string? endpointPath,
        string? displayUrl,
        ExternalMcpClientProfile? externalProfile,
        bool isUserManaged,
        string? discoveryError,
        bool isBuiltInAgentToolEnabled,
        bool isCatalogEnabled,
        bool isEntryEnabled,
        string? disabledReason,
        IReadOnlyList<McpCatalogToolInfo> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(tools);

        Name = name;
        Description = description;
        SourceKind = sourceKind;
        TransportKind = transportKind;
        EndpointPath = endpointPath;
        DisplayUrl = displayUrl;
        ExternalProfile = externalProfile;
        IsUserManaged = isUserManaged;
        DiscoveryError = discoveryError;
        IsBuiltInAgentToolEnabled = isBuiltInAgentToolEnabled;
        IsCatalogEnabled = isCatalogEnabled;
        IsEntryEnabled = isEntryEnabled;
        DisabledReason = disabledReason;
        Tools = tools;
    }

    /// <summary>
    /// Entry name shown to management UIs.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Entry description shown to management UIs.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Entry origin.
    /// </summary>
    public McpCatalogSourceKind SourceKind { get; }

    /// <summary>
    /// Transport for local Monica-defined MCP servers. External MCP clients do not have a Monica-owned transport.
    /// </summary>
    public McpServerTransportKind? TransportKind { get; }

    /// <summary>
    /// Local HTTP route pattern for Monica-defined MCP servers. Non-HTTP and external entries do not have a Monica-owned endpoint path.
    /// </summary>
    public string? EndpointPath { get; }

    /// <summary>
    /// Optional externally reachable URL configured for display in MCP management UIs.
    /// </summary>
    public string? DisplayUrl { get; }

    /// <summary>
    /// External HTTP MCP profile when the entry is backed by a remote client.
    /// </summary>
    public ExternalMcpClientProfile? ExternalProfile { get; }

    /// <summary>
    /// Gets whether this entry can be edited from the runtime management UI.
    /// </summary>
    public bool IsUserManaged { get; }

    /// <summary>
    /// Error captured while discovering remote MCP tools, if discovery failed.
    /// </summary>
    public string? DiscoveryError { get; }

    /// <summary>
    /// Gets whether this entry is configured by its author or registration to expose tools to Monica agents.
    /// </summary>
    public bool IsBuiltInAgentToolEnabled { get; }

    /// <summary>
    /// Gets whether the MCP catalog runtime switch is enabled.
    /// </summary>
    public bool IsCatalogEnabled { get; }

    /// <summary>
    /// Gets whether this specific MCP entry is enabled in runtime settings.
    /// </summary>
    public bool IsEntryEnabled { get; }

    /// <summary>
    /// Gets whether this entry currently contributes tools to Monica agents.
    /// </summary>
    public bool IsAgentToolEnabled => IsBuiltInAgentToolEnabled && IsCatalogEnabled && IsEntryEnabled && DisabledReason is null;

    /// <summary>
    /// UI-localizable reason code or fallback display text explaining why the entry cannot currently be exposed to Monica agents.
    /// </summary>
    public string? DisabledReason { get; }

    /// <summary>
    /// Tools known for this catalog entry.
    /// </summary>
    public IReadOnlyList<McpCatalogToolInfo> Tools { get; }
}
