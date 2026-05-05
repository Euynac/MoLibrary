using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Monica.AI.Mcp.Services;

namespace Monica.AI.Mcp.Internal;

internal sealed class McpServerOptionsConfigurator(
    MonicaMcpCatalog catalog,
    IAgentCapabilityStateStore stateStore) : IConfigureOptions<McpServerOptions>
{
    private readonly Lazy<AgentCapabilityState> _state = new(() =>
        stateStore.LoadAsync().GetAwaiter().GetResult());

    public void Configure(McpServerOptions options)
    {
        catalog.TryConfigureStdioServerOptions(options, _state.Value);
        options.Filters.Request.ListToolsFilters.Add(FilterListTools);
        options.Filters.Request.CallToolFilters.Add(FilterCallTool);
    }

    private McpRequestHandler<ListToolsRequestParams, ListToolsResult> FilterListTools(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            for (var i = result.Tools.Count - 1; i >= 0; i--)
            {
                var tool = result.Tools[i];
                if (tool.Name is null || !IsToolStartupEnabled(context, tool.Name))
                {
                    result.Tools.RemoveAt(i);
                }
            }

            return result;
        };
    }

    private McpRequestHandler<CallToolRequestParams, CallToolResult> FilterCallTool(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            if (!IsToolStartupEnabled(context, context.Params.Name))
            {
                throw new McpProtocolException(
                    $"Tool '{context.Params.Name}' is not available on this MCP server.",
                    McpErrorCode.InvalidParams);
            }

            return await next(context, cancellationToken);
        };
    }

    private bool IsToolStartupEnabled(McpServerTool tool)
    {
        var metadata = tool.Metadata.OfType<McpToolMetadata>().FirstOrDefault();
        return metadata?.SkillName is null
               || _state.Value.IsSkillMcpServerEnabled(
                   metadata.SkillName,
                   metadata.SkillMcpEnabledByDefault);
    }

    private bool IsToolStartupEnabled<TParams>(
        RequestContext<TParams> context,
        string toolName)
    {
        if (context.Server.ServerOptions.ToolCollection?.TryGetPrimitive(toolName, out var primitive) != true)
        {
            return true;
        }

        var metadata = primitive!.Metadata.OfType<McpToolMetadata>().FirstOrDefault();
        return metadata?.SkillName is null
               || _state.Value.IsSkillMcpServerEnabled(
                   metadata.SkillName,
                   metadata.SkillMcpEnabledByDefault);
    }
}
