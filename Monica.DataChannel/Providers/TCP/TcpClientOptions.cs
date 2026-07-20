using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP
{
    /// <summary>
    /// Configures an outbound TCP endpoint for a data-channel pipeline.
    /// </summary>
    public class TcpClientOptions : CommunicationOptions<TcpClientEndpoint>
    {
        /// <summary>
        /// Gets or sets the connection name and resolved TCP address.
        /// </summary>
        public KeyValuePair<string, ConnectedExtend> ClientAddress { get; set; }

        /// <summary>
        /// Gets or sets whether the TCP client worker should run.
        /// </summary>
        public bool IsClient { get; set; }

        /// <summary>
        /// Initializes TCP client options with the supported data direction.
        /// </summary>
        /// <param name="direction">The data direction exposed by the endpoint.</param>
        public TcpClientOptions(ConnectionDirection direction = ConnectionDirection.InputAndOutput)
        {
            Type = CommunicationType.TCP;
            Direction = direction;
        }
    }
}
