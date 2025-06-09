using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Tool.General;

namespace MoLibrary.SignalR.Interfaces;

public abstract class MoHubServer<TIContract>(IMoSignalRConnectionManager connectionManager)
    : Hub<TIContract> where TIContract : class, IMoHubContract
{
    protected static ILogger Logger => LogProvider.For<MoHubServer<TIContract>>();
    public override async Task OnConnectedAsync()
    {
        Logger.LogInformation("客户端：" + Context.ToJsonStringForce());
        if (Context.User?.Identity?.IsAuthenticated == true)
            connectionManager.AddConnection(Context.ConnectionId, Context.User);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connectionManager.RemoveConnection(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}