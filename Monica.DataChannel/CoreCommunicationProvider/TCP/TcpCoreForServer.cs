using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.CoreCommunicationProvider.TCP.Utils;
using Monica.DataChannel.Pipeline;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP
{
    public class TcpCoreForServer(MetadataForTcpServer metadata, ILogger<TcpCoreForServer> logger, IDataChannelManager manager) : CommunicationCore<MetadataForTcpServer>(metadata)
    {
        private TcpServerExtends? server;

        public override async Task ReceiveDataAsync(DataContext data)
        {
            var key = data.Metadata.GetOrDefault("ConnectionName") as string;
            var message = data.Data?.ToString();

            if (!key.IsNullOrEmptySet())
            {
                if (TcpUtils.clients.TryGetValue(key, out var client))
                {
                    await client.SendMsg(message, logger, manager);
                }

                return;
            }

            foreach (var item in TcpUtils.clients.Values)
            {
                await item.SendMsg(message, logger, manager);
            }
        }

        public override Task InitAsync(CancellationToken cancellationToken = default)
        {
            var tcpClientExtends = new TcpServerExtends();
            server = tcpClientExtends;
            server.ReceivedMsgEvent += (e) =>
            {
                var data = CreateData(e.Data);
                data.Metadata.Set("ConnectionName", e.ConnectionName);
                SendData(data);
            };
            server.Init(metadata, logger);
            return Task.CompletedTask;
        }

        public override EConnectionDirection SupportedConnectionDirection()
        {
            return EConnectionDirection.InputAndOutput;
        }

        public override Task DisposeAsync(CancellationToken cancellationToken = default)
        {
            server?.Dispose();
            return base.DisposeAsync(cancellationToken);
        }
    }
}
