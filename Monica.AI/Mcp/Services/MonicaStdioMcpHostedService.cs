using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Runs Monica's MCP server over stdio only when active MCP catalog entries require stdio transport.
/// </summary>
public sealed class MonicaStdioMcpHostedService(
    MonicaMcpCatalog catalog,
    IOptions<McpServerOptions> serverOptions,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime? lifetime = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = serverOptions.Value;
        if (!catalog.HasStdioServers || options.ToolCollection is not { IsEmpty: false })
        {
            return;
        }

        await using var transport = new StdioServerTransport(options, loggerFactory);
        await using var server = McpServer.Create(transport, options, loggerFactory, serviceProvider);

        try
        {
            await server.RunAsync(stoppingToken);
        }
        finally
        {
            lifetime?.StopApplication();
        }
    }
}
