namespace Monica.AI.Mcp.Models;

/// <summary>
/// Describes one tool available from an MCP catalog entry.
/// </summary>
public sealed record McpCatalogToolInfo(
    string Name,
    string Description,
    bool IsAgentToolEnabled);
