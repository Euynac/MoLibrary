using Microsoft.AspNetCore.SignalR;
using MoLibrary.Authority.Security;

namespace BuildingBlocksPlatform.SignalR;

public interface IMoHubContract
{
}

public interface IMoHubOperator<TIContract, TIUser> where TIContract : IMoHubContract where TIUser : IMoCurrentUser
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
    ///     获取连接管理器
    /// </summary>
    IMoSignalRConnectionManager ConnectionManager { get; }

    /// <summary>
    ///     获取所有正在连接的用户
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers();

    /// <summary>
    ///     获取所有满足指定条件的正在连接的用户
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers(Func<SignalRConnectionInfo, TIUser, bool> judge);

    /// <summary>
    ///     获取所有满足指定条件的正在连接的用户
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    IReadOnlyList<TIUser> GetUsers(Predicate<TIUser> judge);

    /// <summary>
    ///     获取指定用户名的正在连接的用户
    /// </summary>
    /// <returns></returns>
    TIUser? GetUser(string username);

    /// <summary>
    ///     给指定用户列表推送消息
    /// </summary>
    /// <param name="users"></param>
    /// <returns></returns>
    TIContract Users(IReadOnlyList<TIUser> users);

    /// <summary>
    ///     根据连接信息给指定用户推送消息
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    TIContract User(SignalRConnectionInfo info);

    /// <summary>
    ///     给指定用户推送消息
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    TIContract User(TIUser user);

    /// <summary>
    ///     给满足指定条件的用户推送消息
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    TIContract Users(Func<SignalRConnectionInfo, TIUser, bool> judge);

    /// <summary>
    ///     给满足指定条件的用户推送消息
    /// </summary>
    /// <param name="judge"></param>
    /// <returns></returns>
    TIContract Users(Predicate<TIUser> judge);

    /// <summary>
    ///     获取所有正在连接的用户，key为SignalR连接ID
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos();
}