using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.TCP;

internal sealed partial class TcpServerExtends
{
    private CancellationTokenSource? _source;
    private string? _serverKey;

    internal void Init(TcpServerOptions metadata, ILogger logger)
    {
        _source = new CancellationTokenSource();
        var cancellationToken = _source.Token;
        _ = Task.Run(() => RunServerLoopAsync(metadata, logger, cancellationToken), cancellationToken);
    }

    private async Task RunServerLoopAsync(
        TcpServerOptions metadata,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var address = metadata.ServerAddress.Value.Address
            ?? throw new InvalidOperationException("TCP server address is not configured.");

        StartServer(metadata.ServerAddress.Key, address.Item2, address.Item1, logger);
        var server = Server ?? throw new InvalidOperationException("TCP server listener is not initialized.");

        while (metadata.IsServer && !cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await server.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var remoteEndpoint = (IPEndPoint?)client.Client.RemoteEndPoint;
            if (remoteEndpoint is null)
            {
                client.Dispose();
                continue;
            }

            logger.LogInformation("Accepted TCP client {RemoteEndpoint}.", remoteEndpoint);
            RegisterAcceptedClient(client, remoteEndpoint, metadata, logger, cancellationToken);
        }
    }

    private void RegisterAcceptedClient(
        TcpClient client,
        IPEndPoint remoteEndpoint,
        TcpServerOptions metadata,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var address = remoteEndpoint.Address.ToString();
        var addressGroup = $"{metadata.ServerAddress.Key}:{GetNetworkGroup(remoteEndpoint.Address)}";
        var connectionName = $"{metadata.ServerAddress.Key}|{address}:{remoteEndpoint.Port}";
        var connection = new TcpClientExtends(_runtime)
        {
            Client = client,
            Connected = true,
            ConnectionName = connectionName
        };
        _runtime.RegisterServerClient(addressGroup, connectionName, connection);
        _ = Task.Run(
            () => _runtime.ReceiveServer(connection, logger, connectionName, ReceivedMsgEvent, cancellationToken),
            cancellationToken);
        _ = Task.Run(
            () => _runtime.SendServerHeartbeatAsync(connection, logger, metadata.SendTime, cancellationToken),
            cancellationToken);
    }

    private void StartServer(string key, int port, string host, ILogger logger)
    {
        if (_runtime.TryGetServer(key, out var existingServer))
        {
            existingServer?.Server?.Stop();
        }

        Server = new TcpListener(IPAddress.Parse(host), port);
        Server.Start();
        _serverKey = key;
        _runtime.SetServer(key, this);
        logger.LogInformation("TCP listener {Key} started on {Host}:{Port}.", key, host, port);
    }

    private static string GetNetworkGroup(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            ? string.Join('.', bytes.Take(3))
            : Convert.ToHexString(bytes.AsSpan(0, Math.Min(8, bytes.Length)));
    }

    public void Dispose()
    {
        _source.SafeCancelAndDispose();
        _source = null;
        Server?.Stop();
        Server = null;
        if (_serverKey is { } serverKey)
        {
            _runtime.UnregisterServer(serverKey, this);
            _serverKey = null;
        }
    }
}
