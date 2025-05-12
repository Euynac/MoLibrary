using Microsoft.AspNetCore.SignalR;
using MoLibrary.Authority.Security;

namespace MoLibrary.SignalR.Implements;

public class MoUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return new MoCurrentUser(connection.User).Id;
    }
}