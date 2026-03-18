using System.Security.Claims;
using Monica.Authority.Identity.Abstractions;
using Monica.Authority.Identity.Services;

namespace Monica.Authority.Identity.Extensions;

public static class MoCurrentUserExtensions
{
    /// <summary>
    /// 转换为当前用户对象
    /// </summary>
    public static IMoCurrentUser AsMoCurrentUser(this ClaimsPrincipal user)
    {
        var currentUser = new MoCurrentUser(user);
        return currentUser;
    }
}