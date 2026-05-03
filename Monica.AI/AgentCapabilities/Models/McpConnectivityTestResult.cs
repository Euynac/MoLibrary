using Monica.AI.Mcp.Models;

namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Result returned after probing an MCP catalog entry.
/// </summary>
public sealed record McpConnectivityTestResult(
    string Name,
    McpCatalogSourceKind SourceKind,
    bool Success,
    string Message,
    TimeSpan Duration,
    int ToolCount,
    DateTimeOffset TestedAt,
    IReadOnlyList<string>? ToolNames = null);
