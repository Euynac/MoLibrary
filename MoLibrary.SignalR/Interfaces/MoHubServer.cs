using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;

namespace MoLibrary.SignalR.Interfaces;

public abstract class MoHubServer<TIContract>(IMoSignalRConnectionManager connectionManager)
    : Hub<TIContract> where TIContract : class, IMoHubContract
{
    protected static ILogger Logger => LogProvider.For<MoHubServer<TIContract>>();
    public override async Task OnConnectedAsync()
    {
        Logger.LogInformation("客户端连接: ConnectionId={ConnectionId}, User={User}, UserIdentifier={UserIdentifier}",
            Context.ConnectionId,
            Context.User?.Identity?.Name ?? "(anonymous)",
            Context.UserIdentifier ?? "(none)");

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