using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Pipeline;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP;

public partial class TcpClientExtends : IDisposable
{
    public bool Connected { get; set; }
    public DateTime? LastSendMsgTime { get; set; }
    public TcpClient? Client { get; set; }
    public TcpReceiveEventHander? MsgReceivedEvent { get; set; }

    /// <summary>
    /// Indicates whether the current client is the primary connection.
    /// </summary>
    public bool IsMainThread { get; set; }
    public string? ConnectionName { get; set; }

    public async Task SendMsg(string? msg, ILogger logger, IDataChannelManager? manager)
    {
        if (!Connected)
        {
            return;
        }

        await TcpUtils.DecideClient(this);
        if (string.IsNullOrEmpty(msg))
        {
            return;
        }

        var client = Client ?? throw new InvalidOperationException("TCP client is not initialized.");
        var rawSendBytes = Encoding.UTF8.GetBytes(msg);
        var stream = client.GetStream();

        try
        {
            client.SendBufferSize = 1024;
            await stream.WriteAsync(rawSendBytes, 0, rawSendBytes.Length);
            LastSendMsgTime = DateTime.Now;
            logger.LogInformation(
                "Send TCP Data [{Length} B] to {ConnectionName}: \r\n{Message}\r\n",
                rawSendBytes.Length,
                ConnectionName,
                msg);
        }
        catch (SocketException ex)
        {
            logger.LogError(ex, "Connection is lost when writing.");
            await HandleSendFailureAsync(msg, manager);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Write to tcp server error.");
            await HandleSendFailureAsync(msg, manager);
            throw;
        }
    }

    public async Task ReSend(string msg, IDataChannelManager? manager)
    {
        var connectionName = ConnectionName;
        if (manager is null || string.IsNullOrEmpty(connectionName))
        {
            return;
        }

        var channel = manager.Fetch(connectionName);
        if (channel is null)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
        await channel.Pipe.SendDataAsync(new ChannelDataContext(ChannelSide.Inner, msg));
    }

    private async Task HandleSendFailureAsync(string msg, IDataChannelManager? manager)
    {
        await ReSend(msg, manager);
        if (IsMainThread)
        {
            TcpUtils.switchoverFlag = true;
            await TcpUtils.DecideClient(this);
            return;
        }

        Connected = false;
    }
}
