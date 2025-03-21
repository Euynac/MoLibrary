using BuildingBlocksPlatform.DataChannel.CoreCommunication;
using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP
{
    public class TcpCoreForClient(MetadataForTcpClient metadata , ILogger<TcpCoreForClient> logger, IDataChannelManager manager) : CommunicationCore<MetadataForTcpClient>(metadata)
    {
        private TcpClientExtends client;


        public override async Task ReceiveDataAsync(DataContext data)
        {
          await  client.SendMsg(data.Data.ToString() ,logger,manager);
         }

        public override async Task InitAsync()
        {
            var tcpClientExtends = new TcpClientExtends();
            client = tcpClientExtends;
            client.MsgReceivedEvent += (e) =>
            {
                SendData(e);
            };
            client.Init(metadata, logger);
           
            
        }
        public override  EConnectionDirection SupportedConnectionDirection()
        {
            return EConnectionDirection.InputAndOutput;
        }

        public override Task DisposeAsync()
        {
            client.Dispose();
            return base.DisposeAsync();
        }
    }
}
