using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.CoreCommunicationProvider.TCP.Utils;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP
{
    public class MetadataForTcpClient : CommunicationMetadata<TcpCoreForClient>
    {

        public KeyValuePair<string, ConnectedExtend> ClientAddress { get; set; }
        public bool IsClient { get; set; }

        public MetadataForTcpClient(EConnectionDirection direction = EConnectionDirection.InputAndOutput)
        {
            Type = ECommunicationType.TCP;
            Direction = direction;
        }
    }
}
