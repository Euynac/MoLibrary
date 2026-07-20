using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Providers.TCP.Utils;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.TCP;

internal sealed partial class TcpClientExtends
{
    private CancellationTokenSource? _source;

    internal void Init(TcpClientOptions metadata, ILogger logger)
    {
        _source = new CancellationTokenSource();
        var cancellationToken = _source.Token;
        _ = Task.Factory.StartNew(
            () => RunClientLoopAsync(metadata, logger, cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task RunClientLoopAsync(
        TcpClientOptions metadata,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var reconnectCount = 0;
        var connectionName = metadata.ClientAddress.Key;

        while (metadata.IsClient && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var address = metadata.ClientAddress.Value.Address
                    ?? throw new InvalidOperationException("TCP client address is not configured.");

                await ConnectAsync(
                    metadata.ClientAddress.Key,
                    address.Item2,
                    address.Item1,
                    logger,
                    metadata.ClientAddress.Value.IsMainConnected,
                    cancellationToken);

                connectionName = ConnectionName ?? metadata.ClientAddress.Key;
                _runtime.ReceiveClient(this, logger, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException exception)
            {
                logger.LogError(exception, "TCP client {ConnectionName} failed to connect.", connectionName);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "TCP client {ConnectionName} failed to initialize.", connectionName);
                throw;
            }

            reconnectCount++;
            logger.LogWarning(
                "TCP client {ConnectionName} disconnected; reconnect attempt {ReconnectCount} will start after a delay.",
                connectionName,
                reconnectCount);
            await Task.Delay(TcpConnectionRuntime.ReconnectDelay, cancellationToken);
        }
    }

    private async Task ConnectAsync(
        string connectionName,
        int port,
        string hostName,
        ILogger logger,
        bool isMainConnection,
        CancellationToken cancellationToken)
    {
        if (_runtime.TryGetClient(connectionName, out var existingClient))
        {
            if (existingClient is not null && IsConnected(existingClient, logger))
            {
                return;
            }

            existingClient?.Client?.Dispose();
        }

        var client = new TcpClient();
        await client.ConnectAsync(hostName, port, cancellationToken);

        Client = client;
        Connected = true;
        IsMainThread = isMainConnection;
        ConnectionName = connectionName;
        _runtime.SetClient(connectionName, this);

        logger.LogInformation(
            "TCP client {ConnectionName} connected to {HostName}:{Port}.",
            connectionName,
            hostName,
            port);
    }

    private static bool IsConnected(TcpClientExtends connection, ILogger logger)
    {
        try
        {
            var socket = connection.Client?.Client;
            if (socket is null || !connection.Connected)
            {
                return false;
            }

            if (!socket.Poll(0, SelectMode.SelectRead))
            {
                return true;
            }

            var buffer = new byte[1];
            return socket.Receive(buffer, SocketFlags.Peek) != 0;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "TCP connection health check failed.");
            return false;
        }
    }

    public void Dispose()
    {
        _source.SafeCancelAndDispose();
        _source = null;
        Client?.Dispose();
        Client = null;
        Connected = false;
        if (!IsServerConnection)
        {
            _runtime.UnregisterOutboundClient(this);
        }
    }
}
