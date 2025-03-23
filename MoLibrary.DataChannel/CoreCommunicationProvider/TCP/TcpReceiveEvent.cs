namespace MoLibrary.DataChannel.CoreCommunicationProvider.TCP
{
    public delegate void TcpReceiveEventHander(MsgReceivedEventArgs args);
    public class MsgReceivedEventArgs
    {
        public byte[] Data { get; set; }
        public string? ConnectionName { get; set; }
    }
}
