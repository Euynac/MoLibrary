using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP
{
    public class TcpClientOptions : CommunicationOptions<TcpClientEndpoint>
    {

        public KeyValuePair<string, ConnectedExtend> ClientAddress { get; set; }
        public bool IsClient { get; set; }

        public TcpClientOptions(ConnectionDirection direction = ConnectionDirection.InputAndOutput)
        {
            Type = CommunicationType.TCP;
            Direction = direction;
        }
    }
}
