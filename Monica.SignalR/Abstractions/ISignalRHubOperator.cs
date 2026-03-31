using Microsoft.AspNetCore.SignalR;
using Monica.Authority.Identity.Abstractions;
using Monica.SignalR.Models;

namespace Monica.SignalR.Abstractions;

/// <summary>
/// Provides strongly typed server-side access to connected SignalR clients and user selections.
/// </summary>
/// <typeparam name="TContract">The typed client contract exposed by the hub.</typeparam>
/// <typeparam name="TUser">The application user projection used by the caller.</typeparam>
public interface ISignalRHubOperator<TContract, TUser>
    where TContract : ISignalRHubContract
    where TUser : ICurrentUser
{
    /// <summary>
    /// Gets a client proxy used to invoke methods on connected clients.
    /// </summary>
    IHubClients<TContract> Clients { get; }

    /// <summary>
    /// Gets the SignalR group manager.
    /// </summary>
    IGroupManager Groups { get; }

    /// <summary>
    /// Gets all distinct connected users.
    /// </summary>
    IReadOnlyList<TUser> GetUsers();

    /// <summary>
    /// Gets connected users that satisfy the supplied connection-aware predicate.
    /// </summary>
    IReadOnlyList<TUser> GetUsers(Func<SignalRConnectionInfo, TUser, bool> predicate);

    /// <summary>
    /// Gets connected users that satisfy the supplied user predicate.
    /// </summary>
    IReadOnlyList<TUser> GetUsers(Predicate<TUser> predicate);

    /// <summary>
    /// Gets the first connected user that matches the supplied user name.
    /// </summary>
    TUser? GetUser(string username);

    /// <summary>
    /// Gets a client proxy targeting the supplied users.
    /// </summary>
    TContract Users(IReadOnlyList<TUser> users);

    /// <summary>
    /// Gets a client proxy targeting the supplied connection.
    /// </summary>
    TContract User(SignalRConnectionInfo connectionInfo);

    /// <summary>
    /// Gets a client proxy targeting the supplied user.
    /// </summary>
    TContract User(TUser user);

    /// <summary>
    /// Gets a client proxy targeting users that satisfy the supplied connection-aware predicate.
    /// </summary>
    TContract Users(Func<SignalRConnectionInfo, TUser, bool> predicate);

    /// <summary>
    /// Gets a client proxy targeting users that satisfy the supplied user predicate.
    /// </summary>
    TContract Users(Predicate<TUser> predicate);

    /// <summary>
    /// Gets a snapshot of all tracked SignalR connections.
    /// </summary>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();

    /// <summary>
    /// Determines whether the supplied user still has at least one active SignalR connection.
    /// </summary>
    bool IsUserStillOnline(TUser user);
}
