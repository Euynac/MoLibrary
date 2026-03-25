using System.Security.Claims;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Services;

namespace Monica.Authority.Identity.Extensions;

public static class CurrentUserExtensions
{
    /// <summary>
    /// 转换为当前用户对象
    /// </summary>
    public static ICurrentUser AsCurrentUser(this ClaimsPrincipal user)
    {
        var currentUser = new CurrentUser(user);
        return currentUser;
    }
}