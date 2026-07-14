using Monica.Authority.Authorization.Abstractions;
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
    /// <param name="checker">The host-owned checker for the permission enum.</param>
    /// <param name="permission">The permission to test.</param>
    /// <returns></returns>
    public static bool IsGranted<TEnum>(
        this ICurrentUserBase user,
        IPermissionBitChecker<TEnum> checker,
        TEnum permission) where TEnum : struct, Enum
    {
        return checker.IsGranted(user.ClaimsPrincipal, permission);
    }

    /// <summary>
    /// Get the permissions granted to the current user
    /// </summary>
    /// <param name="user"></param>
    /// <param name="checker">The host-owned checker for the permission enum.</param>
    /// <returns>The permissions granted to the current user.</returns>
    public static HashSet<TEnum> GrantedList<TEnum>(
        this ICurrentUserBase user,
        IPermissionBitChecker<TEnum> checker) where TEnum : struct, Enum
    {
        return [.. checker.GrantedList(user.ClaimsPrincipal)];
    }

    /// <summary>
    /// Get the granted permissions for the current user within the specified scope
    /// </summary>
    /// <param name="user">The current user whose claims are evaluated.</param>
    /// <param name="checker">The host-owned checker for the permission enum.</param>
    /// <param name="permissionScope">The permissions to include in the result.</param>
    /// <returns>The granted permissions within the supplied scope.</returns>
    public static HashSet<TEnum> GrantedList<TEnum>(
        this ICurrentUserBase user,
        IPermissionBitChecker<TEnum> checker,
        params TEnum[] permissionScope)
        where TEnum : struct, Enum
    {
        return [.. checker.GrantedList(user.ClaimsPrincipal, permissionScope)];
    }
}
