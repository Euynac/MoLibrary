using ModelContextProtocol.Server;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Internal;

internal static class McpTransportClassifier
{
    internal static McpServerTransportKind Classify<TParams>(RequestContext<TParams> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transportTypeName = request.JsonRpcRequest.Context?.RelatedTransport?.GetType().Name;
        return transportTypeName?.Contains("Http", StringComparison.OrdinalIgnoreCase) == true
            ? McpServerTransportKind.Http
            : McpServerTransportKind.Stdio;
    }
}
