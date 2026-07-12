using Monica.AI.Abstractions;

namespace Monica.AI.Mcp.Services;

internal sealed class McpChatAgentContributor(MonicaMcpCatalog catalog) : IAIChatAgentContributor
{
    public async ValueTask ContributeAsync(
        AIChatAgentContributionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tools = await catalog.GetAgentToolsAsync(context.CapabilityState, cancellationToken);
        context.AddTools(tools);
    }
}
