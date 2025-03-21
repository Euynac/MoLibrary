using BuildingBlocksPlatform.DataChannel.CoreCommunication;
using BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP.Utils;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP
{
    public class MetadataForTcpServer : CommunicationMetadata<TcpCoreForServer>
    {
        public KeyValuePair<string, ConnectedExtend> ServerAddress { get; set; }

        public bool IsServer {  get; set; }
        /// <summary>
        /// 发送心跳间隔时间（无数据交互时） 秒
        /// </summary>
        public TimeSpan? SendTime { get; set; }


        public MetadataForTcpServer(EConnectionDirection direction = EConnectionDirection.InputAndOutput)
        {
            Type = ECommunicationType.TCP;
            Direction = direction;
        }
    }


}
