using ModelContextProtocol.Client;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Internal;

internal sealed record ExternalMcpClientEntry(
    ExternalMcpClientProfile Profile,
    IReadOnlyList<McpClientTool> Tools,
    string? DiscoveryError)
{
    internal McpCatalogEntryInfo ToInfo(AgentCapabilityState state)
    {
        var catalogEnabled = state.McpEnabled;
        var entryEnabled = state.IsEntryEnabled(AgentCapabilityKind.Mcp, Profile.Name);
        var disabledReason = McpCapabilityProjection.ResolveDisabledReason(
            Profile.IsAgentToolEnabled,
            catalogEnabled,
            entryEnabled,
            DiscoveryError,
            sourceKind: McpCatalogSourceKind.ExternalClient);
        var isAgentToolEnabled = disabledReason is null;

        return new McpCatalogEntryInfo(
            Profile.Name,
            Profile.Description,
            McpCatalogSourceKind.ExternalClient,
            null,
            null,
            Profile.Endpoint,
            Profile,
            Profile.IsUserManaged,
            DiscoveryError,
            Profile.IsAgentToolEnabled,
            catalogEnabled,
            entryEnabled,
            disabledReason,
            Tools.Select(tool => new McpCatalogToolInfo(
                tool.Name,
                tool.Description,
                isAgentToolEnabled,
                AgentCapabilitySchemaParser.FormatSchema(tool.JsonSchema))).ToList());
    }
}
