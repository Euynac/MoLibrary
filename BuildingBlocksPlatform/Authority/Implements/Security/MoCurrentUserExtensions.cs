using System.Security.Claims;
using BuildingBlocksPlatform.Authority.Implements.Authorization;
using BuildingBlocksPlatform.Authority.Security;

namespace BuildingBlocksPlatform.Authority.Implements.Security;

/// <summary>
/// 相关授权扩展方法
/// </summary>
public static class MoCurrentUserExtensions
{
    /// <summary>
    /// 判断当前用户是否有此权限
    /// </summary>
    /// <param name="user"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    public static bool IsGranted<TEnum>(this IMoCurrentUserBase user, TEnum permission) where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return checker.IsGranted(user.ClaimsPrincipal, permission);
    }

    /// <summary>
    /// 获取当前用户拥有的权限
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static HashSet<TEnum> GrantedList<TEnum>(this IMoCurrentUserBase user) where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return [.. checker.GrantedList<TEnum>(user.ClaimsPrincipal)];
    }

    /// <summary>
    /// 获取在给定范围内已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    public static HashSet<TEnum> GrantedList<TEnum>(this IMoCurrentUserBase user, params TEnum[] permissionScope)
        where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return [.. checker.GrantedList(user.ClaimsPrincipal, permissionScope)];
    }
}