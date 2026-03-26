using Monica.Authority.Authorization.Services.Support;
using Monica.Authority.Identity.Abstractions;

namespace Monica.Authority.Authorization.Extensions;

/// <summary>
/// Authorization-related helper extensions
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Determine whether the current user has the specified permission
    /// </summary>
    /// <param name="user"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    public static bool IsGranted<TEnum>(this ICurrentUserBase user, TEnum permission) where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return checker.IsGranted(user.ClaimsPrincipal, permission);
    }

    /// <summary>
    /// Get the permissions granted to the current user
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static HashSet<TEnum> GrantedList<TEnum>(this ICurrentUserBase user) where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return [.. checker.GrantedList<TEnum>(user.ClaimsPrincipal)];
    }

    /// <summary>
    /// Get the granted permissions for the current user within the specified scope
    /// </summary>
    /// <returns></returns>
    public static HashSet<TEnum> GrantedList<TEnum>(this ICurrentUserBase user, params TEnum[] permissionScope)
        where TEnum : struct, Enum
    {
        var checker = PermissionBitCheckerManager.Singleton;
        return [.. checker.GrantedList(user.ClaimsPrincipal, permissionScope)];
    }
}
