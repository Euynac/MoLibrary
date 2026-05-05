using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Internal;

internal sealed record McpToolMetadata(
    string ServerName,
    McpServerTransportKind TransportKind,
    bool IsLocalToolEnabled,
    string? SkillName = null,
    bool SkillMcpEnabledByDefault = false);
