using System.Net;
using System.Net.Sockets;
using BuildingBlocksPlatform.DataChannel.CoreCommunication;
using BuildingBlocksPlatform.DataChannel.Pipeline;
using Microsoft.Extensions.Logging;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.UDP;

public class UDPCore(MetadataForUDP metadata, ILogger<UDPCore> logger) : CommunicationCore<MetadataForUDP>(metadata)
{
    private UdpClient? _udpClient = null;

    public override async Task ReceiveDataAsync(DataContext data)
    {
        //if (_udpClient != null)
        //{
        //    var result = await _udpClient.ReceiveAsync();
        //    msg.Properties.SetString("Type", data.DataType.ToString()); //设置消息种类
        //    await producer.SendAsync(msg);
        //}
    }

    public override async Task InitAsync()
    {
        try
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(metadata.Address), metadata.Port);
            _udpClient = new UdpClient(endPoint);
            await Task.Factory.StartNew(()=>
            {
                IPEndPoint? remote = null;
                while (true)
                {
                    byte[] bytes = _udpClient.Receive(ref remote);
                    SendData(bytes);
                }
            });
        }
        catch (Exception e)
        {
            logger.LogError("UDP Core 初始化失败，错误:{Exception}", e);
        }
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
} 