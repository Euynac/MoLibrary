using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunicationProvider.TCP.Utils;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP;

public partial class TcpClientExtends
{
    private CancellationTokenSource? _source;
    private CancellationToken cancellation;

    /// <summary>
    /// Initializes the TCP client worker loop.
    /// </summary>
    /// <param name="metadata">The TCP client metadata.</param>
    /// <param name="logger">The logger used for connection lifecycle events.</param>
    public void Init(MetadataForTcpClient metadata, ILogger logger)
    {
        _source = new CancellationTokenSource();
        cancellation = _source.Token;
        _ = Task.Factory.StartNew(
            () => RunClientLoopAsync(metadata, logger, cancellation),
            cancellation,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Current).Unwrap();
    }

    private async Task RunClientLoopAsync(MetadataForTcpClient metadata, ILogger logger, CancellationToken cancellationToken)
    {
        var recvErrorCnt = 0;
        var connectionName = metadata.ClientAddress.Key;

        while (metadata.IsClient)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var value = metadata.ClientAddress.Value;
                var address = value.address ?? throw new InvalidOperationException("TCP client address is not configured.");
                var host = address.Item1;
                var port = address.Item2;

                await GetTcpClient(metadata.ClientAddress.Key, port, host, logger, value.IsMainConnected);
                connectionName = ConnectionName ?? metadata.ClientAddress.Key;
                await TcpUtils.ClientReceive(this, logger, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                logger.LogError(ex, "{ConnectionName}连接TCP Server失败!!!", connectionName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tcp client初始化任务启动失败");
                throw;
            }

            recvErrorCnt++;
            logger.LogError("{ConnectionName} TCP Server连接断开", connectionName);
            logger.LogError("{ConnectionName}任务开始第{ReconnectCount}次重连", connectionName, recvErrorCnt);
            await Task.Delay(TimeSpan.FromSeconds(TcpUtils.WaitConnectInterval));
        }
    }



    public async Task GetTcpClient(string jobKey, int port, string hostName, ILogger logger, bool isMainConnected)
    {
        try
        {
            if (TcpUtils.clients.TryGetValue(jobKey, out var existingClient))
            {
                if (!IsTcpClientConnected(existingClient, logger))
                {
                    existingClient.Client?.Close();
                    var client = new TcpClient(hostName, port);

                    if (!client.Connected)
                    {
                        client.ConnectAsync(hostName, port).Wait(10 * 1000);
                    }

                    logger.LogInformation("{JobKey}相关任务与{HostName}:{Port}重建连接成功...", jobKey, hostName, port);

                    Client = client;
                    Connected = true;
                    ConnectionName = jobKey;
                    TcpUtils.clients[jobKey] = this;
                }

                return;
            }

            var newClient = new TcpClient(hostName, port);
            Client = newClient;
            Connected = true;
            IsMainThread = isMainConnected;
            ConnectionName = jobKey;

            TcpUtils.clients.TryAdd(jobKey, this);
            logger.LogInformation("{JobKey}相关任务与{HostName}:{Port}连接成功...", jobKey, hostName, port);
        }
        catch (ArgumentNullException e)
        {
            logger.LogError(e, "ArgumentNullException");
            throw;
        }
        catch (SocketException e)
        {
            logger.LogError(e, "SocketException");
            throw;
        }
        catch (NullReferenceException e)
        {
            logger.LogError(e, "NullReferenceException");
            throw;
        }
    }


    public bool IsTcpClientConnected(TcpClientExtends clientExtends, ILogger logger)
    {
        try
        {
            if (clientExtends.Client != null && clientExtends.Client.Client != null && clientExtends.Connected)
            {
                // Use Poll to check whether the socket has readable data.
                if (clientExtends.Client.Client.Poll(0, SelectMode.SelectRead))
                {
                    var buff = new byte[1];
                    if (clientExtends.Client.Client.Receive(buff, SocketFlags.Peek) == 0)
                    {
                        logger.LogError("tcp连接已中断");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                return true;
            }
            else
            {
                logger.LogError("tcp连接已中断");
                return false;
            }

        }
        catch (Exception)
        {
            logger.LogError("tcp连接已中断");
            return false;
        }
    }
    public void Dispose()
    {
        _source.SafeCancelAndDispose();
        _source = null;
    }
}

