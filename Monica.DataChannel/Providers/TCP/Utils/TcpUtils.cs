using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Apache.NMS.ActiveMQ.Util.Synchronization;
using Microsoft.Extensions.Logging;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.TCP.Utils
{
    public class TcpUtils
    {
        static public ConcurrentDictionary<string, TcpClientExtends>
          clients = new(); // Tracks TCP client instances.

        static public ConcurrentDictionary<string, TcpServerExtends>
         servers = new(); // Tracks TCP server listeners.

        /// <summary>
        /// Flag that signals a thread switchover is required.
        /// </summary>
        public static bool switchoverFlag = false;

        /// <summary>
        /// Keys representing main thread connections.
        /// </summary>
        public static HashSet<string> mainKeys = new HashSet<string>();

        /// <summary>
        /// Keys for standby thread connections.
        /// </summary>
        public static HashSet<string> standbyKeys = new HashSet<string>();

        private readonly static object _lockObject = new object();

        /// <summary>
        /// Counter used during switchover rotations.
        /// </summary>
        public static int number = 0;

        public static int Counts = 0;

        public static readonly int MaxReadEmptyCnt = 5; // Maximum consecutive empty reads.
        public static readonly double WaitDataInterval = 0.02; // Pause after an empty read, in seconds.
        public static readonly int WaitConnectInterval = 2; // Interval between reconnection attempts, in seconds.

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
            var connectionName = clientExtends.ConnectionName;
            if (string.IsNullOrEmpty(connectionName))
            {
                return;
            }

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

                if (standbyKeys.Contains(connectionName))
                {

                    UpdateClient(clientExtends, clientExtends.Connected, isServer).Await();
                    standbyKeys.Remove(connectionName);
                    number++;


                }
                else
               if (mainKeys.Contains(connectionName))
                {
                    UpdateClient(clientExtends, false, isServer).Await();
                    mainKeys.Remove(connectionName);
                    number++;


                }

            }
        }



        /// <summary>
        /// Parses the configured channels and addresses into connection metadata.
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
                var channel = channels.ElementAtOrDefault(i)
                    ?? throw new InvalidOperationException($"Channel configuration at index {i} is missing.");
                var values = channel.Trim().Split(",", StringSplitOptions.RemoveEmptyEntries);
                var addressValue = validaddress[i].Split(":");
                var hostNameOrAddress = addressValue.ElementAtOrDefault(0)
                    ?? throw new InvalidOperationException($"Address configuration at index {i} is missing a host.");
                IPAddress[] address;
                if (hostNameOrAddress == "0.0.0.0")
                {
                    address  = [IPAddress.Any];
                }
                else
                {
                    address = Dns.GetHostAddresses(hostNameOrAddress);
                }

                string hostname = address.ElementAtOrDefault(0)?.ToString() ?? hostNameOrAddress;
                var portText = addressValue.ElementAtOrDefault(1)
                    ?? throw new InvalidOperationException($"Address configuration at index {i} is missing a port.");
                int port = int.Parse(portText);
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


        private async static Task Receive(TcpClientExtends? clientExtends, ILogger logger, string? connectionName, CancellationToken cancellation, bool isServer = false, TcpReceiveEventHander? hander = null)
        {
            if (clientExtends?.Client?.Client is not Socket client)
            {
                return;
            }

            var recvBytes = Array.Empty<byte>();
            var bytesRead = 0;

            try
            {
                using (var stream = new MemoryStream())
                {
                    var buffer = new byte[2048];
                    // Configure the socket receive timeout so ClientReceive does not block indefinitely.
                    client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5 * 1000);
                    bytesRead = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    //  bytesRead = await client.ReceiveAsync(buffer);
                    if (bytesRead > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead);
                        recvBytes = stream.ToArray();
                        // Handle a single TCP receive operation.
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
                            var remoteEndPoint = client.LocalEndPoint;
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





        public static async Task ServerReceive(TcpClientExtends? clientExtends, ILogger logger, string key, TcpReceiveEventHander? hander, CancellationToken cancellation)
        {

            while (clientExtends?.Connected == true)
            {
                await DecideClient(clientExtends, true);
                await Receive(clientExtends, logger, key, cancellation, true, hander);

            }
        }

        /// <summary>
        /// Sends periodic heartbeat packets from the server.
        /// </summary>
        /// <param name="tcpClientExtends"></param>
        /// <param name="logger"></param>
        /// <param name="sendTime"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static async Task ServerSendSHBT(TcpClientExtends? tcpClientExtends, ILogger logger, TimeSpan? sendTime, string key)
        {
            if (tcpClientExtends is null || sendTime is null)
            {
                return;
            }

            if (tcpClientExtends.LastSendMsgTime == null)
            {
                tcpClientExtends.LastSendMsgTime = DateTime.Now;
            }

            while (tcpClientExtends.Connected)
            {
                var lastSendMsgTime = tcpClientExtends.LastSendMsgTime;
                if (lastSendMsgTime is null)
                {
                    tcpClientExtends.LastSendMsgTime = DateTime.Now;
                    continue;
                }

                if (DateTime.Now - lastSendMsgTime.Value >= sendTime.Value)
                {
                   await tcpClientExtends.SendMsg(await SHBT(), logger, null);
                }
            }

            //throw new NotImplementedException();
        }

        // public static async Task 

        /// <summary>
        /// Builds the heartbeat packet payload.
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
