using Microsoft.Extensions.AI;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.Mcp.Services;

internal sealed class McpAgentToolSource(
    LocalMcpServerCatalog localCatalog,
    ExternalMcpClientPool externalClientPool)
{
    internal async Task<IReadOnlyList<AITool>> GetToolsAsync(
        AgentCapabilityState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.McpEnabled)
        {
            return [];
        }

        var tools = localCatalog.GetAgentTools(state).ToList();
        var externalEntries = await externalClientPool.GetEntriesAsync(cancellationToken);
        tools.AddRange(externalEntries
            .Where(entry => string.IsNullOrWhiteSpace(entry.DiscoveryError)
                            && entry.Profile.IsAgentToolEnabled
                            && state.IsEntryEnabled(AgentCapabilityKind.Mcp, entry.Profile.Name))
            .SelectMany(entry => entry.Tools)
            .Cast<AITool>());

        ValidateUniqueToolNames(tools);
        return tools;
    }

    private static void ValidateUniqueToolNames(IEnumerable<AITool> tools)
    {
        var duplicateNames = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate Monica agent tool names are not allowed: " + string.Join(", ", duplicateNames) + ".");
        }
    }
}
