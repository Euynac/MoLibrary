using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;

namespace Monica.SignalR.Abstractions;

/// <summary>
/// Base class for Monica SignalR hubs that automatically tracks authenticated connections.
/// </summary>
/// <typeparam name="TContract">The typed client contract exposed by the hub.</typeparam>
public abstract class SignalRHub<TContract>(ISignalRConnectionRegistry connectionRegistry)
    : Hub<TContract>
    where TContract : class, ISignalRHubContract
{
    /// <summary>
    /// Gets the logger used by the hub base class.
    /// </summary>
    protected static ILogger Logger => LogManager.For<SignalRHub<TContract>>();

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        Logger.LogInformation(
            "SignalR client connected. ConnectionId={ConnectionId}, User={User}, UserIdentifier={UserIdentifier}",
            Context.ConnectionId,
            Context.User?.Identity?.Name ?? "(anonymous)",
            Context.UserIdentifier ?? "(none)");

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            connectionRegistry.AddConnection(Context.ConnectionId, Context.User);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connectionRegistry.RemoveConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
