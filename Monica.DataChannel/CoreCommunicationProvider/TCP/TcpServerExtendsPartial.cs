using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunicationProvider.TCP.Utils;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP
{
    public partial class TcpServerExtends()
    {
        private CancellationTokenSource? _source;
        private CancellationToken cancellation;

        public async void Init(MetadataForTcpServer metadata, ILogger logger)
        {
            _source = new CancellationTokenSource();
            cancellation = _source.Token;

            while (metadata.IsServer)
            {
                var keyValue = metadata.ServerAddress.Value;
                var addressInfo = keyValue.address ?? throw new InvalidOperationException("TCP server address is not configured.");
                string host = addressInfo.Item1;
                int port = addressInfo.Item2;
                await GetTcpServer(metadata.ServerAddress.Key, port, host, logger);

                var server = Server ?? throw new InvalidOperationException("TCP server listener is not initialized.");
                var client = await server.AcceptTcpClientAsync();
                logger.LogInformation("客户端连接成功");
                var remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
                var address = remoteEndPoint.Address.ToString();
                var lastIndex = address.LastIndexOf(".");

                if (lastIndex != -1)
                {
                    var key = address.Substring(0, lastIndex);
                    var tcpClientExtends = new TcpClientExtends();
                    tcpClientExtends.Client = client;
                    tcpClientExtends.Connected = true;
                    var keys = TcpUtils.clients.Keys.ToList();
                    var connectionName = $"{address}:{remoteEndPoint.Port}";

                    if (keys.Contains(key))
                    {
                        tcpClientExtends.ConnectionName = connectionName;
                        var addressKey = keys.Where(p => p.Contains(key)).Single();
                        if (TcpUtils.ServerMainConnect.TryGetValue($"{addressKey}", out var b))
                        {
                            tcpClientExtends.IsMainThread = b;
                        }
                        else
                        {
                            TcpUtils.ServerMainConnect.TryAdd(connectionName, false);
                        }

                        TcpUtils.clients.TryAdd(connectionName, tcpClientExtends);
                    }
                    else
                    {
                        TcpUtils.ServerMainConnect.TryAdd(connectionName, true);
                        tcpClientExtends.IsMainThread = true;
                        tcpClientExtends.ConnectionName = connectionName;
                        TcpUtils.clients.TryAdd(connectionName, tcpClientExtends);
                    }

                    _ = Task.Run(
                        () => TcpUtils.ServerReceive(tcpClientExtends, logger, connectionName, ReceivedMsgEvent, cancellation),
                        cancellation);

                    //心跳机制
                    _ = Task.Run(
                        () => TcpUtils.ServerSendSHBT(tcpClientExtends, logger, metadata.SendTime, connectionName),
                        cancellation);

                }
                else
                {
                    logger.LogError("client网段获取错误");
                    return;
                }

            }

        }



        public async Task GetTcpServer(string jobKey, int port, string host, ILogger logger)
        {
            try
            {
                if (TcpUtils.servers.TryGetValue(jobKey, out var existingServer))
                {
                    existingServer.Server?.Stop();
                    Server = new TcpListener(IPAddress.Parse(host), port);
                    Server.Start();
                    logger.LogInformation("{JobKey}相关任务与{Host}:{Port}重建并等待连接...", jobKey, host, port);
                    TcpUtils.servers[jobKey] = this;
                }
                else
                {
                    Server = new TcpListener(IPAddress.Parse(host), port);
                    TcpUtils.servers.TryAdd(jobKey, this);
                    logger.LogInformation("{JobKey}相关任务与{Host}:{Port}等待连接...", jobKey, host, port);
                }

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

            Server.Start();

        }








        public void Dispose()
        {
            _source.SafeCancelAndDispose();
            _source = null;
        }
    }
}
