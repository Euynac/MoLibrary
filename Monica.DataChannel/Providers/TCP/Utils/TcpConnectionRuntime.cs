using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Monica.DataChannel.Providers.TCP.Utils;

/// <summary>
/// Owns TCP connections, listeners, and failover coordination for one application host.
/// </summary>
/// <remarks>
/// The DataChannel module registers this type as a singleton in the current host. Its state is never
/// shared with another host in the same process. Application code should consume DataChannel abstractions
/// instead of mutating this provider runtime directly.
/// </remarks>
public sealed class TcpConnectionRuntime
{
    private const int SOCKET_RECEIVE_TIMEOUT_MILLISECONDS = 5_000;
    private static readonly TimeSpan HEARTBEAT_POLL_INTERVAL = TimeSpan.FromSeconds(1);

    private readonly object _failoverLock = new();
    private readonly object _serverGroupLock = new();
    private readonly ConcurrentDictionary<string, TcpClientExtends> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TcpServerExtends> _servers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _outboundClientKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _serverConnectionGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _serverGroupByConnection = new(StringComparer.Ordinal);
    private readonly HashSet<string> _primaryKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _standbyKeys = new(StringComparer.Ordinal);
    private bool _failoverRequested;
    private int _processedConnectionCount;
    private int _targetConnectionCount;

    /// <summary>
    /// Gets the delay used between outbound TCP reconnection attempts.
    /// </summary>
    internal static TimeSpan ReconnectDelay { get; } = TimeSpan.FromSeconds(2);

    internal bool TryGetClient(string key, out TcpClientExtends? client)
    {
        return _clients.TryGetValue(key, out client);
    }

    internal TcpClientExtends[] GetClients()
    {
        return _clients.Values.ToArray();
    }

    internal void SetClient(string key, TcpClientExtends client)
    {
        _clients[key] = client;
        lock (_failoverLock)
        {
            _outboundClientKeys.Add(key);
        }
    }

    internal bool TryGetServer(string key, out TcpServerExtends? server)
    {
        return _servers.TryGetValue(key, out server);
    }

    internal void SetServer(string key, TcpServerExtends server)
    {
        _servers[key] = server;
    }

    internal void UnregisterOutboundClient(TcpClientExtends connection)
    {
        var connectionName = connection.ConnectionName;
        if (connectionName is null)
        {
            return;
        }

        if (_clients.TryGetValue(connectionName, out var registeredConnection) &&
            ReferenceEquals(registeredConnection, connection))
        {
            _clients.TryRemove(connectionName, out _);
        }

        lock (_failoverLock)
        {
            _outboundClientKeys.Remove(connectionName);
            _primaryKeys.Remove(connectionName);
            _standbyKeys.Remove(connectionName);
        }
    }

    internal void UnregisterServer(string serverKey, TcpServerExtends server)
    {
        if (_servers.TryGetValue(serverKey, out var registeredServer) &&
            ReferenceEquals(registeredServer, server))
        {
            _servers.TryRemove(serverKey, out _);
        }

        lock (_serverGroupLock)
        {
            var groupPrefix = $"{serverKey}:";
            var groupKeys = _serverConnectionGroups.Keys
                .Where(key => key.StartsWith(groupPrefix, StringComparison.Ordinal))
                .ToArray();

            foreach (var groupKey in groupKeys)
            {
                var connectionNames = _serverConnectionGroups[groupKey];
                _serverConnectionGroups.Remove(groupKey);

                foreach (var connectionName in connectionNames)
                {
                    _serverGroupByConnection.Remove(connectionName);
                    if (_clients.TryRemove(connectionName, out var connection))
                    {
                        connection.Connected = false;
                        connection.Client?.Dispose();
                    }
                }
            }
        }
    }

    internal void RegisterServerClient(string groupKey, string connectionName, TcpClientExtends connection)
    {
        lock (_serverGroupLock)
        {
            if (!_serverConnectionGroups.TryGetValue(groupKey, out var connections))
            {
                connections = [];
                _serverConnectionGroups.Add(groupKey, connections);
            }

            var isPrimary = connections.Count == 0;
            connections.Add(connectionName);
            _serverGroupByConnection.Add(connectionName, groupKey);
            connection.IsServerConnection = true;
            connection.IsMainThread = isPrimary;
            _clients[connectionName] = connection;
        }
    }

    internal void RequestFailover()
    {
        lock (_failoverLock)
        {
            if (_failoverRequested)
            {
                return;
            }

            _primaryKeys.Clear();
            _standbyKeys.Clear();
            _processedConnectionCount = 0;
            _targetConnectionCount = 0;
            _failoverRequested = true;
        }
    }

    internal void ApplyFailover(TcpClientExtends connection)
    {
        var connectionName = connection.ConnectionName;
        if (string.IsNullOrWhiteSpace(connectionName) || connection.IsServerConnection)
        {
            return;
        }

        lock (_failoverLock)
        {
            if (!_failoverRequested)
            {
                return;
            }

            _targetConnectionCount = _targetConnectionCount == 0 ? _outboundClientKeys.Count : _targetConnectionCount;
            if (_targetConnectionCount == 0 || _processedConnectionCount >= _targetConnectionCount)
            {
                CompleteFailover();
                return;
            }

            if (_primaryKeys.Count == 0 && _standbyKeys.Count == 0)
            {
                PopulateFailoverKeys();
            }

            if (_standbyKeys.Remove(connectionName))
            {
                UpdateOutboundConnection(connection, connection.Connected);
                _processedConnectionCount++;
            }
            else if (_primaryKeys.Remove(connectionName))
            {
                UpdateOutboundConnection(connection, false);
                _processedConnectionCount++;
            }

            if (_processedConnectionCount >= _targetConnectionCount)
            {
                CompleteFailover();
            }
        }
    }

    internal void ReceiveClient(TcpClientExtends connection, ILogger logger, CancellationToken cancellationToken)
    {
        while (connection.Connected && !cancellationToken.IsCancellationRequested)
        {
            ApplyFailover(connection);
            Receive(connection, logger, connection.ConnectionName, isServerConnection: false, handler: null);
        }
    }

    internal void ReceiveServer(
        TcpClientExtends connection,
        ILogger logger,
        string connectionName,
        TcpReceiveEventHander? handler,
        CancellationToken cancellationToken)
    {
        while (connection.Connected && !cancellationToken.IsCancellationRequested)
        {
            Receive(connection, logger, connectionName, isServerConnection: true, handler);
        }
    }

    internal async Task SendServerHeartbeatAsync(
        TcpClientExtends connection,
        ILogger logger,
        TimeSpan? sendInterval,
        CancellationToken cancellationToken)
    {
        if (sendInterval is null)
        {
            return;
        }

        connection.LastSendMsgTime ??= DateTime.UtcNow;
        while (connection.Connected && !cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - connection.LastSendMsgTime.GetValueOrDefault();
            if (elapsed >= sendInterval.Value)
            {
                await connection.SendMsg(CreateHeartbeatPayload(), logger, manager: null);
            }

            await Task.Delay(HEARTBEAT_POLL_INTERVAL, cancellationToken);
        }
    }

    internal void HandleServerDisconnect(TcpClientExtends connection)
    {
        var connectionName = connection.ConnectionName;
        if (connectionName is null)
        {
            return;
        }

        lock (_serverGroupLock)
        {
            _clients.TryRemove(connectionName, out _);
            if (!_serverGroupByConnection.Remove(connectionName, out var groupKey) ||
                !_serverConnectionGroups.TryGetValue(groupKey, out var connections))
            {
                return;
            }

            connections.Remove(connectionName);
            if (connections.Count == 0)
            {
                _serverConnectionGroups.Remove(groupKey);
                return;
            }

            if (!connection.IsMainThread)
            {
                return;
            }

            var nextPrimary = connections
                .Select(name => _clients.GetValueOrDefault(name))
                .FirstOrDefault(candidate => candidate?.Connected == true);
            if (nextPrimary is not null)
            {
                nextPrimary.IsMainThread = true;
            }
        }
    }

    private void PopulateFailoverKeys()
    {
        foreach (var key in _outboundClientKeys)
        {
            if (_clients.TryGetValue(key, out var connection))
            {
                (connection.IsMainThread ? _primaryKeys : _standbyKeys).Add(key);
            }
        }
    }

    private void UpdateOutboundConnection(TcpClientExtends connection, bool connected)
    {
        var connectionName = connection.ConnectionName;
        if (connectionName is null || !_clients.TryGetValue(connectionName, out var registeredConnection))
        {
            return;
        }

        registeredConnection.Connected = connected;
        registeredConnection.IsMainThread = !connection.IsMainThread;
    }

    private void CompleteFailover()
    {
        _primaryKeys.Clear();
        _standbyKeys.Clear();
        _processedConnectionCount = 0;
        _targetConnectionCount = 0;
        _failoverRequested = false;
    }

    private void Receive(
        TcpClientExtends connection,
        ILogger logger,
        string? connectionName,
        bool isServerConnection,
        TcpReceiveEventHander? handler)
    {
        if (connection.Client?.Client is not Socket socket)
        {
            return;
        }

        try
        {
            var buffer = new byte[2048];
            socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReceiveTimeout,
                SOCKET_RECEIVE_TIMEOUT_MILLISECONDS);

            var bytesRead = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
            if (bytesRead == 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            var receivedBytes = buffer[..bytesRead];
            if (connection.IsMainThread)
            {
                var eventArgs = new MsgReceivedEventArgs
                {
                    Data = receivedBytes,
                    ConnectionName = connectionName
                };

                if (isServerConnection)
                {
                    handler?.Invoke(eventArgs);
                }
                else
                {
                    connection.MsgReceivedEvent?.Invoke(eventArgs);
                }

                logger.LogInformation(
                    "Primary TCP connection {ConnectionName} received {Message}",
                    connectionName,
                    Encoding.UTF8.GetString(receivedBytes).Trim());
                return;
            }

            logger.LogDebug(
                "Standby TCP connection {ConnectionName} received and ignored {Message}",
                connectionName,
                Encoding.UTF8.GetString(receivedBytes).Trim());
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
        {
            // A receive timeout is a polling boundary, not a failed connection.
        }
        catch (SocketException exception)
        {
            logger.LogWarning(exception, "TCP connection {ConnectionName} was interrupted.", connectionName);
            connection.Connected = false;
            if (isServerConnection)
            {
                HandleServerDisconnect(connection);
            }
            else if (connection.IsMainThread)
            {
                RequestFailover();
                ApplyFailover(connection);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to receive data from TCP connection {ConnectionName}.", connectionName);
        }
    }

    private static string CreateHeartbeatPayload()
    {
        return "ZCZC\r\n" +
               "-TITLE SHBT\r\n" +
               "-BEGIN REFDATA\r\n" +
               "-SENDER -FAC ZTMA\r\n" +
               "-RECVR -FAC ZUUU\r\n" +
               "-END REFDATA\r\n" +
               "NNNN";
    }
}
