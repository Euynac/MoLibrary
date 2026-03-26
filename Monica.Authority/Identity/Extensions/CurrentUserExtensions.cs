using System.Security.Claims;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Services;

namespace Monica.Authority.Identity.Extensions;

public static class CurrentUserExtensions
{
    /// <summary>
    /// Converts the principal into a current user abstraction
    /// </summary>
    public static ICurrentUser AsCurrentUser(this ClaimsPrincipal user)
    {
        var currentUser = new CurrentUser(user);
        return currentUser;
    }
}
