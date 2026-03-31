using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Extensions;
using Monica.SignalR.Models;

namespace Monica.SignalR.Abstractions;

/// <summary>
/// Default hub operator that projects SignalR connection principals into <see cref="ICurrentUser"/>.
/// </summary>
/// <typeparam name="TContract">The typed client contract exposed by the hub.</typeparam>
/// <typeparam name="THub">The hub type.</typeparam>
public class CurrentUserSignalRHubOperator<TContract, THub>(
    IHubContext<THub, TContract> hubContext,
    ISignalRConnectionRegistry connectionRegistry)
    : SignalRHubOperator<TContract, THub, ICurrentUser>(hubContext, connectionRegistry)
    where TContract : class, ISignalRHubContract
    where THub : SignalRHub<TContract>
{
    /// <inheritdoc />
    protected override ICurrentUser ConvertToCurrentUser(ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal.AsCurrentUser();
    }
}

/// <summary>
/// Base class for strongly typed server-side SignalR operators.
/// </summary>
/// <typeparam name="TContract">The typed client contract exposed by the hub.</typeparam>
/// <typeparam name="THub">The hub type.</typeparam>
/// <typeparam name="TUser">The application user projection used by the caller.</typeparam>
public abstract class SignalRHubOperator<TContract, THub, TUser>(
    IHubContext<THub, TContract> hubContext,
    ISignalRConnectionRegistry connectionRegistry)
    : ISignalRHubOperator<TContract, TUser>
    where TContract : class, ISignalRHubContract
    where THub : SignalRHub<TContract>
    where TUser : ICurrentUser
{
    /// <inheritdoc />
    public IHubClients<TContract> Clients => hubContext.Clients;

    /// <inheritdoc />
    public IGroupManager Groups => hubContext.Groups;

    /// <inheritdoc />
    public IReadOnlyList<TUser> GetUsers()
    {
        return connectionRegistry
            .GetConnectionInfos()
            .Select(connectionInfo => ConvertToCurrentUser(connectionInfo.ClaimsPrincipal))
            .DistinctBy(user => user.Id)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TUser> GetUsers(Func<SignalRConnectionInfo, TUser, bool> predicate)
    {
        return connectionRegistry
            .GetConnectionInfos()
            .Where(connectionInfo =>
            {
                var user = ConvertToCurrentUser(connectionInfo.ClaimsPrincipal);
                return predicate(connectionInfo, user);
            })
            .Select(connectionInfo => ConvertToCurrentUser(connectionInfo.ClaimsPrincipal))
            .DistinctBy(user => user.Id)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TUser> GetUsers(Predicate<TUser> predicate)
    {
        return GetUsers().Where(user => predicate(user)).ToList();
    }

    /// <inheritdoc />
    public TUser? GetUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return default;
        }

        return connectionRegistry
            .GetConnectionInfos()
            .Select(connectionInfo => ConvertToCurrentUser(connectionInfo.ClaimsPrincipal))
            .FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public TContract Users(IReadOnlyList<TUser> users)
    {
        var userIds = users
            .Select(user => user.Id)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Clients.Users(userIds);
    }

    /// <inheritdoc />
    public TContract User(SignalRConnectionInfo connectionInfo)
    {
        return User(ConvertToCurrentUser(connectionInfo.ClaimsPrincipal));
    }

    /// <inheritdoc />
    public TContract User(TUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("The target user does not expose a stable Id.");
        }

        return Clients.User(user.Id);
    }

    /// <inheritdoc />
    public TContract Users(Func<SignalRConnectionInfo, TUser, bool> predicate)
    {
        return Users(GetUsers(predicate));
    }

    /// <inheritdoc />
    public TContract Users(Predicate<TUser> predicate)
    {
        return Users(GetUsers(predicate));
    }

    /// <inheritdoc />
    public IReadOnlyList<SignalRConnectionInfo> GetConnectionInfos()
    {
        return connectionRegistry.GetConnectionInfos();
    }

    /// <inheritdoc />
    public bool IsUserStillOnline(TUser user)
    {
        return GetConnectionInfos().Any(connectionInfo =>
        {
            var connectedUser = ConvertToCurrentUser(connectionInfo.ClaimsPrincipal);
            return string.Equals(connectedUser.Id, user.Id, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Converts the connection principal into the user projection used by the caller.
    /// </summary>
    protected abstract TUser ConvertToCurrentUser(ClaimsPrincipal claimsPrincipal);
}
