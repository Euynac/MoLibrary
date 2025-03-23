using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.CoreCommunicationProvider.TCP.Utils;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.TCP
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
