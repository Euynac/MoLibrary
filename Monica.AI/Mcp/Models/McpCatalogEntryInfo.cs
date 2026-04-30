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
        bool isAgentToolEnabled,
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
        IsAgentToolEnabled = isAgentToolEnabled;
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
    /// Gets whether this entry contributes tools to Monica agents.
    /// </summary>
    public bool IsAgentToolEnabled { get; }

    /// <summary>
    /// Tools known for this catalog entry.
    /// </summary>
    public IReadOnlyList<McpCatalogToolInfo> Tools { get; }
}
