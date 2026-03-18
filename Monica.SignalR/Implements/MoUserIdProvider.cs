using Microsoft.AspNetCore.SignalR;
using Monica.Authority.Identity.Services;

namespace Monica.SignalR.Implements;

public class MoUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return new MoCurrentUser(connection.User).Id;
    }
}