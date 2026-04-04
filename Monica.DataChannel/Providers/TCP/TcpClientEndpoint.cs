using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.TCP
{
    public class TcpClientEndpoint(TcpClientOptions metadata , ILogger<TcpClientEndpoint> logger, IDataChannelManager manager) : CommunicationEndpointBase<TcpClientOptions>(metadata)
    {
        private TcpClientExtends? client;

        public override Task ReceiveDataAsync(ChannelDataContext data) =>
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

        public override  ConnectionDirection SupportedConnectionDirection()
        {
            return ConnectionDirection.InputAndOutput;
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
