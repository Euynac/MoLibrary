using System.Net.Sockets;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.TCP;

public partial class TcpServerExtends : IDisposable
{

    public TcpListener? Server { get; set; }


    public TcpReceiveEventHander ReceivedMsgEvent;
}

