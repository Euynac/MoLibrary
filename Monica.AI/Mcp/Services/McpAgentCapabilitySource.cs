using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Internal;

namespace Monica.AI.Mcp.Services;

internal sealed class McpAgentCapabilitySource(MonicaMcpCatalog catalog) : IAgentCapabilitySource
{
    public AgentCapabilityKind Kind => AgentCapabilityKind.Mcp;

    public async Task<AgentCapabilitySourceSnapshot> GetSnapshotAsync(
        AgentCapabilityState state,
        CancellationToken ct = default)
    {
        var entries = await catalog.GetEntriesAsync(state, ct);
        return new AgentCapabilitySourceSnapshot(
            entries.Select(McpCapabilityProjection.ToCapabilityInfo).ToList());
    }

    public Task InvalidateAsync(CancellationToken ct = default)
    {
        return catalog.InvalidateExternalEntriesAsync(ct);
    }
}
