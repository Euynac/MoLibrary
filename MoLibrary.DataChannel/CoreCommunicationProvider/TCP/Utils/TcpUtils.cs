using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Apache.NMS.ActiveMQ.Util.Synchronization;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.TCP.Utils
{
    public class TcpUtils
    {
        static public ConcurrentDictionary<string, TcpClientExtends>
          clients = new(); //维护TCP客户端

        static public ConcurrentDictionary<string, TcpServerExtends>
         servers = new(); //维护TCP服务端

        /// <summary>
        /// 切换链路标志
        /// </summary>
        public static bool switchoverFlag = false;

        /// <summary>
        /// 主线程key
        /// </summary>
        public static HashSet<string> mainKeys = new HashSet<string>();

        /// <summary>
        /// 备用线程key
        /// </summary>
        public static HashSet<string> standbyKeys = new HashSet<string>();

        private readonly static object _lockObject = new object();

        /// <summary>
        /// 计算器
        /// </summary>
        public static int number = 0;

        public static int Counts = 0;

        public static readonly int MaxReadEmptyCnt = 5; // 接收内容为空最大次数
        public static readonly double WaitDataInterval = 0.02; // 接收为空后休眠时间，单位秒
        public static readonly int WaitConnectInterval = 2; // 等数数据库重连时间，单位秒

        public static ConcurrentDictionary<string, bool> ServerMainConnect = new();





        public static async Task InitServerThreadKey()
        {
            foreach (var item in ServerMainConnect)
            {

                if (item.Value)
                {
                    mainKeys.Add(item.Key);
                }
                if (!item.Value)
                {
                    standbyKeys.Add(item.Key);
                }
            }
        }
        public static async Task InitClientThreadKey()
        {
            foreach (var item in clients)
            {
                if (item.Value.IsMainThread)
                {
                    mainKeys.Add(item.Key);
                }
                if (!item.Value.IsMainThread)
                {
                    standbyKeys.Add(item.Key);
                }
            }
        }


        public static async Task UpdateClient(TcpClientExtends extends, bool connected, bool isServer)
        {
            if (clients.TryGetValue(extends.ConnectionName!, out var clientExtends))
            {
                if (isServer)
                {
                    if (connected==false)
                    {
                        clients.Remove(extends.ConnectionName!, out var res);
                    }
                    else
                    {
                        clientExtends.Connected = connected;
                        clientExtends.IsMainThread =!extends.IsMainThread;
                    }
                }
                else
                {
                    clientExtends.Connected = connected;
                    clientExtends.IsMainThread =!extends.IsMainThread;
                }
            }
        }
        public static long i = 0;
        public static async Task DecideClient(TcpClientExtends clientExtends, bool isServer = false)
        {
            if (!switchoverFlag) return;

            lock (_lockObject)
            {

                if (!switchoverFlag) return;

                i++;
                Console.WriteLine(i);
                if (Counts ==0)
                {
                    Counts =  clients.Count;
                }
                if (number >= Counts)
                {
                    number = 0;
                    Counts=0;
                    switchoverFlag = false;
                    return;
                }
                if (switchoverFlag && mainKeys.IsNullOrEmptySet() && standbyKeys.IsNullOrEmptySet())
                {
                    if (isServer)
                    {
                        InitServerThreadKey().Await();
                    }
                    else
                    {
                        InitClientThreadKey().Await();
                    }
                }

                if (standbyKeys.Contains(clientExtends.ConnectionName))
                {

                    UpdateClient(clientExtends, clientExtends.Connected, isServer).Await();
                    standbyKeys.Remove(clientExtends.ConnectionName);
                    number++;


                }
                else
               if (mainKeys.Contains(clientExtends.ConnectionName))
                {
                    UpdateClient(clientExtends, false, isServer).Await();
                    mainKeys.Remove(clientExtends.ConnectionName);
                    number++;


                }

            }
        }



        /// <summary>
        /// 配置处理
        /// </summary>
        /// <param name="channels"></param>
        /// <param name="validaddress"></param>
        /// <returns></returns>
        public static ConcurrentDictionary<string, ConnectedExtend> ParseAddress(List<string> channels, List<string> validaddress)
        {
            ConcurrentDictionary<string, ConnectedExtend> addressDict = new ConcurrentDictionary<string, ConnectedExtend>();

            for (int i = 0; i<channels.Count; i++)
            {
                var connectedExtend = new ConnectedExtend();
                var values = channels.ElementAtOrDefault(i).Trim().Split(",", StringSplitOptions.RemoveEmptyEntries);
                var addressValue = validaddress[i].Split(":");
                IPAddress[] address = null;
                if (addressValue.ElementAtOrDefault(0)  == "0.0.0.0")
                {
                    address  = [IPAddress.Any];
                }
                else
                {
                    address = Dns.GetHostAddresses(addressValue.ElementAtOrDefault(0));
                }

                string hostname = address.ElementAtOrDefault(0).ToString();
                int port = int.Parse(addressValue.ElementAtOrDefault(1));
                connectedExtend.address =  new Tuple<string, int>(hostname, port);
                connectedExtend.IsMainConnected = values.ElementAtOrDefault(2) == "1" ? true : false;
                var extend = $"{values.ElementAtOrDefault(1)}{values.ElementAtOrDefault(2)}";
                var key = extend.IsNullOrWhiteSpace()?  values.ElementAtOrDefault(0) : values.ElementAtOrDefault(0) + ":" + extend;
                addressDict.TryAdd($"{key}", connectedExtend);
            }

            return addressDict;

        }


        public static async Task ClientReceive(TcpClientExtends? clientExtends, ILogger logger, CancellationToken cancellation)
        {
            while (clientExtends?.Connected ==true)
            {
                await DecideClient(clientExtends);
                await Receive(clientExtends, logger, clientExtends.ConnectionName, cancellation);
            }

        }


        private async static Task Receive(TcpClientExtends? clientExtends, ILogger logger, string? connectionName, CancellationToken cancellation, bool isServer = false, TcpReceiveEventHander hander = null)
        {

            var client = clientExtends.Client.Client;
            var recvBytes = new byte[0];
            var bytesRead = 0;

            try
            {
                using (var stream = new MemoryStream())
                {
                    var buffer = new byte[2048];
                    // 设置客户端接收超时时间,否则将一直等待服务端回复
                    client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5 * 1000);
                    bytesRead = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    //  bytesRead = await client.ReceiveAsync(buffer);
                    if (bytesRead > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead);
                        recvBytes = stream.ToArray();
                        // 读一次TCP数据接收后的处理
                        if (clientExtends.IsMainThread)
                        {
                            if (isServer)
                            {
                                hander?.Invoke(new MsgReceivedEventArgs()
                                {
                                    Data = recvBytes,
                                    ConnectionName = connectionName,
                                });
                            }
                            else
                            {
                                clientExtends.MsgReceivedEvent?.Invoke(new MsgReceivedEventArgs()
                                {
                                    Data = recvBytes,
                                    ConnectionName = connectionName,
                                });
                            }
                            logger.LogInformation($"主线程：{connectionName} 接收的消息 {Encoding.UTF8.GetString(recvBytes, 0, bytesRead).Trim()}");
                        }
                        else
                        {
                            var remoteEndPoint = clientExtends.Client.Client.LocalEndPoint;
                            logger.LogInformation($"备用线程：{remoteEndPoint}");
                            logger.LogInformation($"备用线程：{connectionName} 接收的消息 {Encoding.UTF8.GetString(recvBytes, 0, bytesRead).Trim()}");
                            return;
                        }

                    }
                    else
                    {
                        throw new SocketException();
                    }
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.TimedOut)
                {
                    if (clientExtends.IsMainThread)
                    {
                        if (isServer)
                        {
                            clientExtends.Connected = false;
                            switchoverFlag = true;
                            await DecideClient(clientExtends, true);
                        }
                        else
                        {
                            switchoverFlag = true;
                            await DecideClient(clientExtends);
                        }

                    }
                    else
                    {
                        clientExtends.Connected = false;
                    }

                }
                return;
            }
            catch (Exception ex)
            {
                logger!.LogError($"Recv from tcp server error,{ex.Message}");
                return;
            }
        }





        public static async Task ServerReceive(TcpClientExtends? clientExtends, ILogger logger, string key, TcpReceiveEventHander hander, CancellationToken cancellation)
        {

            while (clientExtends?.Connected == true)
            {
                await DecideClient(clientExtends, true);
                await Receive(clientExtends, logger, key, cancellation, true, hander);

            }
        }

        /// <summary>
        /// 心跳机制
        /// </summary>
        /// <param name="tcpClientExtends"></param>
        /// <param name="logger"></param>
        /// <param name="sendTime"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static async Task ServerSendSHBT(TcpClientExtends? tcpClientExtends, ILogger logger, TimeSpan? sendTime, string key)
        {
            if (tcpClientExtends?.LastSendMsgTime == null)
            {
                tcpClientExtends.LastSendMsgTime = DateTime.Now;
            }
            while (tcpClientExtends?.Connected == true)
            {
                if (DateTime.Now - tcpClientExtends.LastSendMsgTime >= sendTime)
                {
                   await tcpClientExtends.SendMsg(await SHBT(), logger, null);
                }
            }

            //throw new NotImplementedException();
        }

        // public static async Task 

        /// <summary>
        /// 心跳包
        /// </summary>
        /// <returns></returns>
        private  static async Task<string> SHBT()
        {
            return "ZCZC\r\n" +
                "-TITLE SHBT\r\n" +
                "-BEGIN REFDATA\r\n"+
                "-SENDER -FAC ZTMA\r\n" +
                "-RECVR -FAC ZUUU\r\n" +
                "-END REFDATA\r\n" +
                "NNNN";
        }


    }
    public class ConnectedExtend
    {
        public Tuple<string, int>? address { get; set; }
        public bool IsMainConnected { get; set; }
    }
}
