using System.Diagnostics;
using ModelContextProtocol.Client;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Services;

internal sealed class McpConnectivityService(
    LocalMcpServerCatalog localCatalog,
    ExternalMcpProfileCatalog externalProfileCatalog,
    ExternalMcpClientFactory clientFactory)
{
    internal async Task<McpConnectivityTestResult?> TestAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var localEntry = localCatalog.Find(name);
        if (localEntry is not null)
        {
            return await TestLocalAsync(localEntry, cancellationToken);
        }

        var profile = (await externalProfileCatalog.GetProfilesAsync(cancellationToken)).FirstOrDefault(entry =>
            string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        return profile is null
            ? null
            : await TestExternalAsync(profile, cancellationToken);
    }

    internal Task<McpConnectivityTestResult> TestExternalAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return TestExternalCoreAsync(profile.Normalize(profile.Origin), cancellationToken);
    }

    private static async Task<McpConnectivityTestResult> TestLocalAsync(
        LocalMcpServerEntry entry,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var testedAt = DateTimeOffset.UtcNow;
        if (entry.TransportKind == McpServerTransportKind.Http
            && Uri.TryCreate(entry.HttpDisplayUrl, UriKind.Absolute, out var endpoint))
        {
            try
            {
                await using var transport = new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = entry.Definition.Name,
                    Endpoint = endpoint
                });
                await using var client = await McpClient.CreateAsync(
                    transport,
                    loggerFactory: null,
                    cancellationToken: cancellationToken);
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                return CreateResult(
                    entry.Definition.Name,
                    entry.SourceKind,
                    true,
                    McpCapabilityMessageCode.Connectivity.ConnectedToEndpoint,
                    startedAt,
                    testedAt,
                    tools.Select(static tool => tool.Name));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return CreateResult(
                    entry.Definition.Name,
                    entry.SourceKind,
                    false,
                    ex.Message,
                    startedAt,
                    testedAt,
                    []);
            }
        }

        var message = entry.TransportKind == McpServerTransportKind.Http
            ? McpCapabilityMessageCode.Connectivity.LocalHttpDisplayUrlMissing
            : McpCapabilityMessageCode.Connectivity.LocalStdioInProcessReady;
        return CreateResult(
            entry.Definition.Name,
            entry.SourceKind,
            true,
            message,
            startedAt,
            testedAt,
            entry.Tools.Select(static tool => tool.Name));
    }

    private async Task<McpConnectivityTestResult> TestExternalCoreAsync(
        ExternalMcpClientProfile profile,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var testedAt = DateTimeOffset.UtcNow;
        try
        {
            await using var client = await clientFactory.CreateAsync(profile, cancellationToken);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            return CreateResult(
                profile.Name,
                McpCatalogSourceKind.ExternalClient,
                true,
                McpCapabilityMessageCode.Connectivity.ConnectedAndListedTools,
                startedAt,
                testedAt,
                tools.Select(static tool => tool.Name));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CreateResult(
                profile.Name,
                McpCatalogSourceKind.ExternalClient,
                false,
                ex.Message,
                startedAt,
                testedAt,
                []);
        }
    }

    private static McpConnectivityTestResult CreateResult(
        string name,
        McpCatalogSourceKind sourceKind,
        bool success,
        string message,
        long startedAt,
        DateTimeOffset testedAt,
        IEnumerable<string> toolNames)
    {
        var orderedNames = toolNames.OrderBy(static name => name, StringComparer.Ordinal).ToList();
        return new McpConnectivityTestResult(
            name,
            sourceKind,
            success,
            message,
            Stopwatch.GetElapsedTime(startedAt),
            orderedNames.Count,
            testedAt,
            orderedNames);
    }
}
