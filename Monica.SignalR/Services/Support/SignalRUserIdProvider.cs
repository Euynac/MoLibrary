using Microsoft.AspNetCore.SignalR;
using Monica.Authority.Identity.Services;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Resolves SignalR user identifiers from Monica's current user abstraction.
/// </summary>
internal sealed class SignalRUserIdProvider : IUserIdProvider
{
    /// <inheritdoc />
    public string? GetUserId(HubConnectionContext connection)
    {
        return new CurrentUser(connection.User).Id;
    }
}
