using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Pipeline;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP;

internal sealed partial class TcpClientExtends : IDisposable
{
    private readonly TcpConnectionRuntime _runtime;

    internal TcpClientExtends(TcpConnectionRuntime runtime)
    {
        _runtime = runtime;
    }

    internal bool Connected { get; set; }
    internal DateTime? LastSendMsgTime { get; set; }
    internal TcpClient? Client { get; set; }
    internal TcpReceiveEventHander? MsgReceivedEvent { get; set; }
    internal bool IsMainThread { get; set; }
    internal bool IsServerConnection { get; set; }
    internal string? ConnectionName { get; set; }

    internal async Task SendMsg(string? message, ILogger logger, IDataChannelManager? manager)
    {
        if (!Connected || string.IsNullOrEmpty(message))
        {
            return;
        }

        _runtime.ApplyFailover(this);

        var client = Client ?? throw new InvalidOperationException("TCP client is not initialized.");
        var bytes = Encoding.UTF8.GetBytes(message);

        try
        {
            client.SendBufferSize = 1024;
            await client.GetStream().WriteAsync(bytes);
            LastSendMsgTime = DateTime.UtcNow;
            logger.LogInformation(
                "Sent {Length} byte(s) to TCP connection {ConnectionName}: {Message}",
                bytes.Length,
                ConnectionName,
                message);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            logger.LogError(exception, "Failed to write to TCP connection {ConnectionName}.", ConnectionName);
            await HandleSendFailureAsync(message, manager);
            throw;
        }
    }

    private async Task HandleSendFailureAsync(string message, IDataChannelManager? manager)
    {
        if (IsServerConnection)
        {
            Connected = false;
            _runtime.HandleServerDisconnect(this);
            return;
        }

        var connectionName = ConnectionName;
        if (manager is not null && !string.IsNullOrEmpty(connectionName))
        {
            var channel = manager.Fetch(connectionName);
            if (channel is not null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                await channel.Pipe.SendDataAsync(new ChannelDataContext(ChannelSide.Inner, message));
            }
        }

        if (IsMainThread)
        {
            _runtime.RequestFailover();
            _runtime.ApplyFailover(this);
        }
        else
        {
            Connected = false;
        }
    }
}
