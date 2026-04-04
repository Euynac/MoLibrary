using System.Net.Sockets;

namespace Monica.DataChannel.Providers.TCP;

public partial class TcpServerExtends : IDisposable
{
    public TcpListener? Server { get; set; }
    public TcpReceiveEventHander? ReceivedMsgEvent { get; set; }
}

