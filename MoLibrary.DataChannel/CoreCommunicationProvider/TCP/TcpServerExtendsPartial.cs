using System.Net;
using System.Net.Sockets;
using BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP.Utils;
using Microsoft.Extensions.Logging;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP
{
    public partial class TcpServerExtends()
    {
        private CancellationTokenSource? _source;
        private CancellationToken cancellation;
        public async void Init(MetadataForTcpServer metadata, ILogger logger)
        {
            _source = new CancellationTokenSource();

            while (metadata.IsServer)
            {

                var keyValue = metadata.ServerAddress.Value;
                string host = keyValue!.address!.Item1;
                int port = keyValue!.address.Item2;
                await GetTcpServer(metadata.ServerAddress.Key, port, host, logger);

                var client = await Server!.AcceptTcpClientAsync();
                logger.LogInformation("客户端连接成功");
                var remoteEndPoint = (IPEndPoint) client.Client.RemoteEndPoint!;
                var address = remoteEndPoint.Address.ToString();
                var lastIndex = address.LastIndexOf(".");

                if (lastIndex != -1)
                {
                    var key = address.Substring(0, (int) lastIndex!);
                    var tcpClientExtends = new TcpClientExtends();
                    tcpClientExtends.Client = client;
                    tcpClientExtends.Connected = true;
                    var keys = TcpUtils.clients.Keys.ToList();
                   if (keys.Contains(key))
                    {
                        tcpClientExtends.ConnectionName = $"{address}:{remoteEndPoint?.Port}";
                        var addressKey = keys.Where(p => p.Contains(key)).Single();
                        if (TcpUtils.ServerMainConnect.TryGetValue($"{addressKey}", out var b))
                        {
                            tcpClientExtends.IsMainThread = b;
                        }
                        else
                        {
                            TcpUtils.ServerMainConnect.TryAdd($"{address}:{remoteEndPoint?.Port}", false);
                        }

                        TcpUtils.clients.TryAdd($"{address}:{remoteEndPoint?.Port}", tcpClientExtends);
                    }
                    else
                    {
                        //if (TcpUtils.clients.IsNullOrEmpty())
                        //{

                        //    TcpUtils.ServerMainConnect.TryAdd($"{address}:{remoteEndPoint?.Port}", true);
                        //    tcpClientExtends.IsMainThread = true;
                        //}
                        //else
                        //{
                        //    TcpUtils.ServerMainConnect.TryAdd($"{address}:{remoteEndPoint?.Port}", false);
                        //    tcpClientExtends.IsMainThread = false;
                        //}
                        TcpUtils.ServerMainConnect.TryAdd($"{address}:{remoteEndPoint?.Port}", true);
                        tcpClientExtends.IsMainThread = true;
                        tcpClientExtends.ConnectionName = $"{address}:{remoteEndPoint?.Port}";
                        TcpUtils.clients.TryAdd($"{address}:{remoteEndPoint?.Port}", tcpClientExtends);

                    }
                    Task.Factory.StartNew(() => TcpUtils.ServerReceive(tcpClientExtends, logger, $"{address}:{remoteEndPoint?.Port}", ReceivedMsgEvent,cancellation));
                  
                    //心跳机制
                    Task.Factory.StartNew(() => TcpUtils.ServerSendSHBT(tcpClientExtends, logger, metadata.SendTime , $"{address}:{remoteEndPoint?.Port}"));


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

                if (TcpUtils.servers.Keys.Contains(jobKey))
                {
                    TcpUtils.servers[jobKey].Server!.Stop();
                    Server = new TcpListener(IPAddress.Parse(host), port);
                    Server.Start();
                    logger!.LogInformation($"{jobKey}相关任务与{host}:{port}重建并等待连接...");
                    TcpUtils.servers[jobKey] = this;
                }
                else
                {

                    Server = new TcpListener(IPAddress.Parse(host), port); ;
                    TcpUtils.servers.TryAdd(jobKey, this);
                    logger!.LogInformation($"{jobKey}相关任务与{host}:{port}等待连接...");
                }

            }
            catch (ArgumentNullException e)
            {
                logger!.LogError("ArgumentNullException: {0}", e);
                throw e;
            }
            catch (SocketException e)
            {
                logger!.LogError("SocketException: {0}", e.Message);
                throw e;
            }
            catch (NullReferenceException e)
            {
                logger!.LogError("NullReferenceException: {0}", e.Message);
                throw e;
            }
           
            Server.Start();

        }








        public void Dispose()
        {

            if (_source != null)
            {
                _source.Cancel();
            }
        }
    }
}