using System.Security.Claims;
using BuildingBlocksPlatform.Authority.Security;

namespace BuildingBlocksPlatform.Authority.Implements.Security;

/// <summary>
/// 相关授权扩展方法
/// </summary>
public static class OurCurrentUserExtensions
{
    /// <summary>
    /// 转换为当前用户对象
    /// </summary>
    public static IOurCurrentUser AsCurrentUser(this ClaimsPrincipal user)
    {
        var currentUser = new OurCurrentUser(user);
        return currentUser;
    }
}