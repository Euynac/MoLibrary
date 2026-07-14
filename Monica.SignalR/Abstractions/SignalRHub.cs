using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Monica.SignalR.Abstractions;

/// <summary>
/// Base class for Monica SignalR hubs that automatically tracks authenticated connections.
/// </summary>
/// <typeparam name="TContract">The typed client contract exposed by the hub.</typeparam>
/// <param name="connectionRegistry">The host registry that tracks authenticated connections.</param>
/// <param name="logger">The host-owned logger for this hub contract.</param>
public abstract class SignalRHub<TContract>(
    ISignalRConnectionRegistry connectionRegistry,
    ILogger logger)
    : Hub<TContract>
    where TContract : class, ISignalRHubContract
{
    /// <summary>
    /// Gets the logger used by the hub base class.
    /// </summary>
    protected ILogger Logger => logger;

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
