using Microsoft.AspNetCore.SignalR;
using Monica.Authority.Identity.Abstractions;

namespace Monica.SignalR.Interfaces;

public interface IMoHubContract
{
}

public interface IMoHubOperator<TIContract, TIUser> where TIContract : IMoHubContract where TIUser : ICurrentUser
{
    /// <summary>
    ///     Gets a <see cref="T:Microsoft.AspNetCore.SignalR.IHubClients`1" /> that can be used to invoke methods on clients
    ///     connected to the hub.
    /// </summary>
    IHubClients<TIContract> Clients { get; }

    /// <summary>
    ///     Gets a <see cref="T:Microsoft.AspNetCore.SignalR.IGroupManager" /> that can be used to add and remove connections
    ///     to named groups.
    /// </summary>
    IGroupManager Groups { get; }

    /// <summary>
    /// Get connection manager
    /// </summary>
    IMoSignalRConnectionManager ConnectionManager { get; }

    /// <summary>
    /// Get all currently connected users
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers();

    /// <summary>
    /// Get all connected users who meet the specified conditions
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers(Func<SignalRConnectionInfo, TIUser, bool> judge);

    /// <summary>
    /// Get all connected users who meet the specified conditions
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers(Predicate<TIUser> judge);

    /// <summary>
    /// Get the connecting user with the specified username
    /// </summary>
    /// <returns></returns>
    TIUser? GetUser(string username);

    /// <summary>
    /// Push messages to specified user list
    /// </summary>
    /// <param name="users"></param>
    /// <returns></returns>
    TIContract Users(IReadOnlyList<TIUser> users);

    /// <summary>
    /// Push messages to specified users based on connection information
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    TIContract User(SignalRConnectionInfo info);

    /// <summary>
    /// Push messages to specified users
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    TIContract User(TIUser user);

    /// <summary>
    /// Push messages to users who meet specified conditions
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    TIContract Users(Func<SignalRConnectionInfo, TIUser, bool> judge);

    /// <summary>
    /// Push messages to users who meet specified conditions
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    TIContract Users(Predicate<TIUser> judge);

    /// <summary>
    /// Get all connecting users, the key is SignalR connection ID
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();
    
    /// <summary>
    /// Determine whether the current user is still online
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    bool IsUserStillOnline(TIUser user);
}