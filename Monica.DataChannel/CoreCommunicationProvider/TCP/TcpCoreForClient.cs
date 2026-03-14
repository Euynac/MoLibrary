using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP
{
    public class TcpCoreForClient(MetadataForTcpClient metadata , ILogger<TcpCoreForClient> logger, IDataChannelManager manager) : CommunicationCore<MetadataForTcpClient>(metadata)
    {
        private TcpClientExtends? client;

        public override Task ReceiveDataAsync(DataContext data) =>
            GetClient().SendMsg(data.Data?.ToString(), logger, manager);

        public override Task InitAsync(CancellationToken cancellationToken = default)
        {
            var tcpClientExtends = new TcpClientExtends();
            client = tcpClientExtends;
            client.MsgReceivedEvent += (e) =>
            {
                SendData(e);
            };
            client.Init(metadata, logger);
            return Task.CompletedTask;
        }

        public override  EConnectionDirection SupportedConnectionDirection()
        {
            return EConnectionDirection.InputAndOutput;
        }

        public override Task DisposeAsync(CancellationToken cancellationToken = default)
        {
            client?.Dispose();
            return base.DisposeAsync(cancellationToken);
        }

        private TcpClientExtends GetClient() =>
            client ?? throw new InvalidOperationException("TCP client is not initialized.");
    }
}
