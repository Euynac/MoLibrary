using System.Net.Sockets;

namespace Monica.DataChannel.CoreCommunicationProvider.TCP;

public partial class TcpServerExtends : IDisposable
{

    public TcpListener? Server { get; set; }


    public TcpReceiveEventHander ReceivedMsgEvent;
}

