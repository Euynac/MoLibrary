using System.Net.Sockets;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP;

internal sealed partial class TcpServerExtends : IDisposable
{
    private readonly TcpConnectionRuntime _runtime;

    internal TcpServerExtends(TcpConnectionRuntime runtime)
    {
        _runtime = runtime;
    }

    internal TcpListener? Server { get; set; }
    internal TcpReceiveEventHander? ReceivedMsgEvent { get; set; }
}
