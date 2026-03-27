using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.CoreCommunicationProvider.TCP.Utils;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP
{
    public class MetadataForTcpServer : CommunicationMetadata<TcpCoreForServer>
    {
        public KeyValuePair<string, ConnectedExtend> ServerAddress { get; set; }

        public bool IsServer {  get; set; }
        /// <summary>
        /// Interval for sending heartbeats when no data is flowing, in seconds.
        /// </summary>
        public TimeSpan? SendTime { get; set; }


        public MetadataForTcpServer(EConnectionDirection direction = EConnectionDirection.InputAndOutput)
        {
            Type = ECommunicationType.TCP;
            Direction = direction;
        }
    }


}
