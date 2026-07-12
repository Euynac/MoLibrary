using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Services;

internal sealed class ExternalMcpClientPool : IAsyncDisposable
{
    private readonly ExternalMcpProfileCatalog _profileCatalog;
    private readonly ExternalMcpClientFactory _clientFactory;
    private readonly ILogger<ExternalMcpClientPool> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<ExternalMcpClientEntry>? _cachedEntries;
    private IReadOnlyList<McpClient> _cachedClients = [];
    private volatile bool _disposed;

    internal ExternalMcpClientPool(
        ExternalMcpProfileCatalog profileCatalog,
        ExternalMcpClientFactory clientFactory,
        ILogger<ExternalMcpClientPool> logger)
    {
        _profileCatalog = profileCatalog;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    internal async Task<IReadOnlyList<ExternalMcpClientEntry>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cachedEntries is not null)
        {
            return _cachedEntries;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cachedEntries is not null)
            {
                return _cachedEntries;
            }

            var profiles = await _profileCatalog.GetProfilesAsync(cancellationToken);
            var discovered = await DiscoverAsync(profiles, cancellationToken);
            _cachedEntries = discovered.Entries;
            _cachedClients = discovered.Clients;
            return _cachedEntries;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<McpClient> clients;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cachedEntries = null;
            clients = _cachedClients;
            _cachedClients = [];
        }
        finally
        {
            _gate.Release();
        }

        await DisposeClientsAsync(clients);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        IReadOnlyList<McpClient> clients;
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cachedEntries = null;
            clients = _cachedClients;
            _cachedClients = [];
        }
        finally
        {
            _gate.Release();
        }

        await DisposeClientsAsync(clients);
    }

    private async Task<DiscoverySnapshot> DiscoverAsync(
        IReadOnlyList<ExternalMcpClientProfile> profiles,
        CancellationToken cancellationToken)
    {
        var entries = new List<ExternalMcpClientEntry>();
        var clients = new List<McpClient>();
        try
        {
            foreach (var profile in profiles)
            {
                McpClient? client = null;
                try
                {
                    client = await _clientFactory.CreateAsync(profile, cancellationToken);
                    var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
                    clients.Add(client);
                    client = null;
                    entries.Add(new ExternalMcpClientEntry(profile, tools.ToList(), null));
                }
                catch (OperationCanceledException)
                {
                    if (client is not null)
                    {
                        await client.DisposeAsync();
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    if (client is not null)
                    {
                        await client.DisposeAsync();
                    }

                    _logger.LogWarning(
                        ex,
                        "Failed to discover tools for external MCP client profile '{Name}'.",
                        profile.Name);
                    entries.Add(new ExternalMcpClientEntry(profile, [], ex.Message));
                }
            }

            return new DiscoverySnapshot(entries, clients);
        }
        catch
        {
            await DisposeClientsAsync(clients);
            throw;
        }
    }

    private static async Task DisposeClientsAsync(IEnumerable<McpClient> clients)
    {
        foreach (var client in clients)
        {
            await client.DisposeAsync();
        }
    }

    private sealed record DiscoverySnapshot(
        IReadOnlyList<ExternalMcpClientEntry> Entries,
        IReadOnlyList<McpClient> Clients);
}
