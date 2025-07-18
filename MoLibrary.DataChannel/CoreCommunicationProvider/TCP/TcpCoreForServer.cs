using Microsoft.Extensions.Logging;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.CoreCommunicationProvider.TCP.Utils;
using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.TCP
{
    public class TcpCoreForServer(MetadataForTcpServer metadata, ILogger<TcpCoreForServer> logger, IDataChannelManager manager) : CommunicationCore<MetadataForTcpServer>(metadata)
    {
        private TcpServerExtends server;
       // private Timer heartbeat;

        public override async Task ReceiveDataAsync(DataContext data)
        {

           // string id = "";
            var key = data.Metadata.GetOrDefault("ConnectionName") as string;

            if (!key.IsNullOrEmptySet() )
            {
                if (TcpUtils.clients.TryGetValue(key, out var client))
                {
                    await client.SendMsg(data.Data.ToString(), logger, manager);
                }
            }
            else
            {
                foreach (var item in TcpUtils.clients.Values)
                {
                    await item.SendMsg(data.Data.ToString(), logger, manager);
                }
            }
        }

        public override async Task InitAsync(CancellationToken cancellationToken = default)
        {
            var tcpClientExtends = new TcpServerExtends();
      //      heartbeat = new Timer(TimerCallback, null , 0, 10000);
            server = tcpClientExtends;
            server.ReceivedMsgEvent += (e) =>
            {
                var data = CreateData(e.Data);
                data.Metadata.Set("ConnectionName", e.ConnectionName);
                SendData(data);
            };
            server.Init(metadata, logger);

        }


        /// <summary>
        /// timer的回调方法 state 传的是null
        /// </summary>
        /// <param name="state"></param>
        //private async  void TimerCallback(object state)
        //{
        //    foreach (var item in TcpUtils.clients)
        //    {
        //        TcpClientExtends extends;
        //        if(item.Value.LastSendMsgTime == null)
        //        {
        //            extends= item.Value;
        //            extends.LastSendMsgTime = DateTime.Now;
        //            TcpUtils.clients.TryUpdate(item.Key, extends, item.Value);
        //        }
               
        //    }
        //}
        public override EConnectionDirection SupportedConnectionDirection()
        {
            return EConnectionDirection.InputAndOutput;
        }
    }
}
