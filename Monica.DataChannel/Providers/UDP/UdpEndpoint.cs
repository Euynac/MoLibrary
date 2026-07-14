using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.UDP;

public class UdpEndpoint(UdpOptions metadata, ILogger<UdpEndpoint> logger) : CommunicationEndpointBase<UdpOptions>(metadata)
{
    private UdpClient? _udpClient = null;

    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        //if (_udpClient != null)
        //{
        //    var result = await _udpClient.ReceiveAsync();
        //    msg.Properties.SetString("Type", data.DataType.ToString()); // Sets the message type.
        //    await producer.SendAsync(msg);
        //}
    }

    public override async Task InitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(metadata.Address), metadata.Port);
            _udpClient = new UdpClient(endPoint);
            await Task.Factory.StartNew(async ()=>
            {
                while (true)
                {
                    var bytes = await _udpClient.ReceiveAsync();
                   
                    SendData(bytes.Buffer);
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError("UDP endpoint initialization failed. Error: {Exception}", e);
        }
    }

    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }
}
