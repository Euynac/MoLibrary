using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.General;
using Microsoft.AspNetCore.SignalR;

namespace BuildingBlocksPlatform.SignalR;

public abstract class MoHubServer<TIContract>(IMoSignalRConnectionManager connectionManager)
    : Hub<TIContract> where TIContract : class, IMoHubContract
{
    public override async Task OnConnectedAsync()
    {
        GlobalLog.LogInformation("客户端：" + Context.ToJsonStringForce());
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