using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP
{
    public class TcpServerOptions : CommunicationOptions<TcpServerEndpoint>
    {
        public KeyValuePair<string, ConnectedExtend> ServerAddress { get; set; }

        public bool IsServer {  get; set; }
        /// <summary>
        /// Interval for sending heartbeats when no data is flowing, in seconds.
        /// </summary>
        public TimeSpan? SendTime { get; set; }


        public TcpServerOptions(ConnectionDirection direction = ConnectionDirection.InputAndOutput)
        {
            Type = CommunicationType.TCP;
            Direction = direction;
        }
    }


}
