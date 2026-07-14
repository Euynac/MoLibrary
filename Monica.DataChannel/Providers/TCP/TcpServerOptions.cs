using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP
{
    /// <summary>
    /// Configures a TCP listener endpoint for a data-channel pipeline.
    /// </summary>
    public class TcpServerOptions : CommunicationOptions<TcpServerEndpoint>
    {
        /// <summary>
        /// Gets or sets the listener name and resolved TCP address.
        /// </summary>
        public KeyValuePair<string, ConnectedExtend> ServerAddress { get; set; }

        /// <summary>
        /// Gets or sets whether the TCP listener worker should run.
        /// </summary>
        public bool IsServer {  get; set; }
        /// <summary>
        /// Gets or sets the interval for sending heartbeats when no data is flowing.
        /// </summary>
        public TimeSpan? SendTime { get; set; }

        /// <summary>
        /// Initializes TCP server options with the supported data direction.
        /// </summary>
        /// <param name="direction">The data direction exposed by the endpoint.</param>
        public TcpServerOptions(ConnectionDirection direction = ConnectionDirection.InputAndOutput)
        {
            Type = CommunicationType.TCP;
            Direction = direction;
        }
    }


}
