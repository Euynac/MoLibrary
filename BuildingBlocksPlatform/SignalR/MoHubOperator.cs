using System.Security.Claims;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.AspNetCore.SignalR;

namespace BuildingBlocksPlatform.SignalR;

public class MoUserHubOperator<TIContract, THubServer>(
    IHubContext<THubServer, TIContract> hub,
    IMoSignalRConnectionManager connection) : MoHubOperator<TIContract, THubServer, IMoCurrentUser>(hub, connection)
    where TIContract : class, IMoHubContract where THubServer : MoHubServer<TIContract>
{
    public override IMoCurrentUser ConvertToCurrentUser(ClaimsPrincipal cp)
    {
        return cp.AsMoCurrentUser();
    }
}

public abstract class MoHubOperator<TIContract, THubServer, TIUser>(
    IHubContext<THubServer, TIContract> hub,
    IMoSignalRConnectionManager connection) : IMoHubOperator<TIContract, TIUser>
    where TIUser : IMoCurrentUser where TIContract : class, IMoHubContract where THubServer : MoHubServer<TIContract>
{
    public IHubClients<TIContract> Clients => hub.Clients;
    public IGroupManager Groups => hub.Groups;
    public IMoSignalRConnectionManager ConnectionManager => connection;

    public IReadOnlyList<TIUser> GetUsers()
    {
        return ConnectionManager.GetConnectionInfos().Select(p => ConvertToCurrentUser(p.ClaimsPrincipal))
            .DistinctBy(p => p.Id).ToList();
    }

    public IReadOnlyList<TIUser> GetUsers(Func<SignalRConnectionInfo, TIUser, bool> judge)
    {
        return ConnectionManager.GetConnectionInfos().Where(p => judge(p, ConvertToCurrentUser(p.ClaimsPrincipal)))
            .Select(p => ConvertToCurrentUser(p.ClaimsPrincipal)).DistinctBy(p => p.Id).ToList();
    }

    public IReadOnlyList<TIUser> GetUsers(Predicate<TIUser> judge)
    {
        return GetUsers().Where(p => judge(ConvertToCurrentUser(p.ClaimsPrincipal))).ToList();
    }

    public TIUser? GetUser(string username)
    {
        return ConnectionManager.GetConnectionInfos().Select(p => ConvertToCurrentUser(p.ClaimsPrincipal))
            .FirstOrDefault(p => p.Username == username);
    }

    public TIContract Users(IReadOnlyList<TIUser> users)
    {
        var ids = users.Where(p => p.Id != null).Select(p => p.Id!).ToList();
        return Clients.Users(ids);
    }

    public TIContract User(SignalRConnectionInfo info)
    {
        return User(ConvertToCurrentUser(info.ClaimsPrincipal));
    }

    public TIContract User(TIUser user)
    {
        return Clients.User(user.Id!);
    }

    public TIContract Users(Func<SignalRConnectionInfo, TIUser, bool> judge)
    {
        var users = GetUsers(judge);
        return Users(users);
    }

    public TIContract Users(Predicate<TIUser> judge)
    {
        var users = GetUsers(judge);
        return Users(users);
    }

    public IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos()
    {
        return connection.GetConnectionInfos();
    }

    public abstract TIUser ConvertToCurrentUser(ClaimsPrincipal cp);
}