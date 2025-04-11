using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using MoLibrary.DataChannel.CoreCommunicationProvider.TCP.Utils;
using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.TCP;

public partial class TcpClientExtends : IDisposable
{
    public bool Connected { get; set; }

    public DateTime? LastSendMsgTime { get; set; }


    public TcpClient? Client { get; set; }

    public TcpReceiveEventHander MsgReceivedEvent { get; set; }

    /// <summary>
    /// 是否主线程
    /// </summary>
    public bool IsMainThread { get; set; }


    public string? ConnectionName { get; set; }



    public async Task SendMsg(string? msg,ILogger logger , IDataChannelManager? manager)
    {
        var readEmptyCnt = 0;
        var stream = Client!.GetStream();

        if (Connected)
        {
            await TcpUtils.DecideClient(this);
            if (msg.IsNullOrEmptySet())
            {
                return;
            }

            var rawSendBytes = Encoding.UTF8.GetBytes(msg);
  
            if (rawSendBytes != null)
            {
                try
                {
                    Client!.SendBufferSize = 1024;
                    await stream.WriteAsync(rawSendBytes, 0, rawSendBytes.Length);
                    LastSendMsgTime = DateTime.Now;
                    logger!.LogInformation($"Send TCP Data [{rawSendBytes.Length} B] to {ConnectionName}: \r\n{msg}\r\n");
                }
                catch (SocketException e)
                {
                    logger!.LogError("Connection is lost when writing...");
                    await ReSend(msg, manager);
                    if (IsMainThread)
                    {
                       TcpUtils. switchoverFlag = true;
                        await TcpUtils. DecideClient(this);
                    }
                    else
                    {
                        Connected = false;
                    }
                    throw new SocketException();
                }
                catch (Exception e)
                {
                    logger!.LogError($"Write to tcp server error,{e.Message}");
                     await ReSend(msg, manager);
                    if (IsMainThread)
                    {
                        TcpUtils.switchoverFlag = true;
                        await TcpUtils.DecideClient(this);
                    }
                    else
                    {
                        Connected = false;
                    }
                    throw new Exception();
                }
            }
            else
            {
                readEmptyCnt++;
                if (readEmptyCnt > TcpUtils. MaxReadEmptyCnt)
                {
                    await Task.Delay(TimeSpan.FromSeconds(TcpUtils.WaitDataInterval));
                }
            }
        }
    }

    public async Task ReSend(string msg , IDataChannelManager? manager)
    {
        if (manager == null) return;
        

        
        var c = manager.Fetch(ConnectionName!);
        if (c != null)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await c.Pipe.SendDataAsync(new DataContext(EDataSource.Inner, msg));
        }


    }

}
