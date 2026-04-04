using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;
using Monica.DataChannel.Providers.TCP.Utils;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.TCP
{
    public class TcpServerEndpoint(TcpServerOptions metadata, ILogger<TcpServerEndpoint> logger, IDataChannelManager manager) : CommunicationEndpointBase<TcpServerOptions>(metadata)
    {
        private TcpServerExtends? server;

        public override async Task ReceiveDataAsync(ChannelDataContext data)
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

        public override ConnectionDirection SupportedConnectionDirection()
        {
            return ConnectionDirection.InputAndOutput;
        }

        public override Task DisposeAsync(CancellationToken cancellationToken = default)
        {
            server?.Dispose();
            return base.DisposeAsync(cancellationToken);
        }
    }
}
