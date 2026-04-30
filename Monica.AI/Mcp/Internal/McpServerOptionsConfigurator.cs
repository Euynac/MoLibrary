using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;

namespace Monica.AI.Mcp.Internal;

internal sealed class McpServerOptionsConfigurator(MonicaMcpCatalog catalog) : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions options)
    {
        var tools = catalog.GetMcpServerTools();
        if (tools.Count == 0)
        {
            return;
        }

        options.ServerInfo ??= catalog.CreateServerImplementation();
        options.ServerInstructions ??= catalog.CreateServerInstructions();
        options.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>();

        foreach (var tool in tools)
        {
            options.ToolCollection.Add(tool);
        }

        options.Filters.Request.ListToolsFilters.Add(FilterListTools);
        options.Filters.Request.CallToolFilters.Add(FilterCallTool);
    }

    private static McpRequestHandler<ListToolsRequestParams, ListToolsResult> FilterListTools(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var transportKind = McpTransportClassifier.Classify(context);
            var result = await next(context, cancellationToken);

            for (var i = result.Tools.Count - 1; i >= 0; i--)
            {
                var tool = result.Tools[i];
                if (tool.Name is null || !IsToolAllowedForTransport(context, tool.Name, transportKind))
                {
                    result.Tools.RemoveAt(i);
                }
            }

            return result;
        };
    }

    private static McpRequestHandler<CallToolRequestParams, CallToolResult> FilterCallTool(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var transportKind = McpTransportClassifier.Classify(context);
            if (!IsToolAllowedForTransport(context, context.Params.Name, transportKind))
            {
                throw new McpProtocolException(
                    $"Tool '{context.Params.Name}' is not available on the {transportKind} MCP transport.",
                    McpErrorCode.InvalidParams);
            }

            return await next(context, cancellationToken);
        };
    }

    private static bool IsToolAllowedForTransport<TParams>(
        RequestContext<TParams> context,
        string toolName,
        McpServerTransportKind transportKind)
    {
        if (context.Server.ServerOptions.ToolCollection?.TryGetPrimitive(toolName, out var primitive) != true)
        {
            return true;
        }

        return primitive!.Metadata.OfType<McpToolMetadata>().FirstOrDefault()?.TransportKind == transportKind;
    }
}
