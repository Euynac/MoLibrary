namespace Monica.DataChannel.Providers.TCP
{
    public delegate void TcpReceiveEventHander(MsgReceivedEventArgs args);

    public class MsgReceivedEventArgs
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public string? ConnectionName { get; set; }
    }
}
