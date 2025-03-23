using System.Net.Sockets;
using BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP.Utils;
using Microsoft.Extensions.Logging;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP;

public partial class TcpClientExtends
{
    private CancellationTokenSource? _source;
    private CancellationToken cancellation;

    public TcpClientExtends()
    {

    }

    /// <summary>
    /// 初始化client
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public void Init(MetadataForTcpClient metadata, ILogger logger)
    {
        _source = new CancellationTokenSource();
        Task.Factory.StartNew(async () =>
        {
            int RecvErrorCnt = 0;
            string connectionName = "";
            while (metadata.IsClient)
            {
                if (cancellation.IsCancellationRequested) break;

                try
                {
                    var value = metadata.ClientAddress.Value;
                    string host = value.address!.Item1;
                    int port = value.address!.Item2;
                    await GetTcpClient(metadata.ClientAddress.Key, port, host, logger, value.IsMainConnected);
                    await TcpUtils.ClientReceive(this, logger , cancellation);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    logger!.LogError($"{connectionName}连接TCP Server失败!!! {ex.Message}");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Tcp client初始化任务启动失败: {0}", ex.Message);
                    throw ex;

                }

                RecvErrorCnt++;
                logger!.LogError($"{connectionName} TCP Server连接断开");
                logger!.LogError($"{connectionName}任务开始第{RecvErrorCnt}次重连");

                await Task.Delay(TimeSpan.FromSeconds(TcpUtils.WaitConnectInterval));


            }
        }, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current);
    }



    public async Task GetTcpClient(string jobKey, int port, string hostName, ILogger logger, bool isMainConnected)
    {

        TcpClient client;
        try
        {
            if (TcpUtils.clients.Keys.Contains(jobKey))
            {
                if (!IsTcpClientConnected(TcpUtils.clients[jobKey], logger))
                {
                    TcpUtils.clients[jobKey].Client!.Close();
                    client= new TcpClient(hostName, port);

                    if (!client.Connected)
                        client.ConnectAsync(hostName, port).Wait(10 * 1000);
                    logger!.LogInformation($"{jobKey}相关任务与{hostName}:{port}重建连接成功...");

                    Client = client;
                    Connected = true;
                    ConnectionName = jobKey;
                    TcpUtils.clients[jobKey]  = this;
                }
            }
            else
            {
                client = new TcpClient(hostName, port);
                Client = client;
                Connected = true;
                IsMainThread = isMainConnected;
                ConnectionName = jobKey;

                TcpUtils.clients.TryAdd(jobKey, this);
                logger!.LogInformation($"{jobKey}相关任务与{hostName}:{port}连接成功...");
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
       
    }


    public bool IsTcpClientConnected(TcpClientExtends clientExtends, ILogger logger)
    {
        try
        {
            if (clientExtends.Client != null && clientExtends.Client.Client != null && clientExtends.Connected)
            {
                //使用poll方法检查socket的状态 poll 检查是否有可读数据
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
        catch (Exception e)
        {
            logger.LogError("tcp连接已中断");
            return false;

        }
    }
    public void Dispose()
    {

        if (_source != null)
        {
            _source.Cancel();
        }
    }
}

