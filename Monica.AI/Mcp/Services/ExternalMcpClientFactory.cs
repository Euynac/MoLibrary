using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Creates connected MCP clients from serializable external HTTP client profiles.
/// </summary>
internal sealed class ExternalMcpClientFactory(ILoggerFactory loggerFactory)
{
    /// <summary>
    /// Creates and connects an MCP client for the provided external profile.
    /// </summary>
    public async Task<McpClient> CreateAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = profile.Normalize(profile.Origin);
        normalized.Validate();

        var options = new HttpClientTransportOptions
        {
            Name = normalized.Name,
            Endpoint = new Uri(normalized.Endpoint),
            TransportMode = normalized.TransportMode,
            AdditionalHeaders = normalized.Headers
        };

        if (normalized.ConnectionTimeoutSeconds > 0)
        {
            options.ConnectionTimeout = TimeSpan.FromSeconds(normalized.ConnectionTimeoutSeconds);
        }

        var transport = new HttpClientTransport(options, loggerFactory);
        try
        {
            return await McpClient.CreateAsync(
                transport,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken);
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }
}
