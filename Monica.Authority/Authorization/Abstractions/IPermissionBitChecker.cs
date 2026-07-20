using System.Security.Claims;
using Monica.Authority.Authorization.Annotations;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// Binary permission bit checker
/// </summary>
public interface IPermissionBitChecker<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// Determine whether the permission name exists within the provided permission bits string.
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, string permissionNameOfEnum);
    /// <summary>
    /// Determine whether the permission enum entry exists within the provided permission bits string.
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, TEnum permissionEnum);
    /// <summary>
    /// Determine whether all provided permission enums exist within the given permission bits string.
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, params TEnum[] permissionEnums);
    /// <summary>
    /// Determine whether the permission name exists within the ClaimsPrincipal's permission bits string.
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, string permissionNameOfEnum);
    /// <summary>
    /// Determine whether the permission enum exists within the ClaimsPrincipal's permission bits string.
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, TEnum permissionEnum);
    /// <summary>
    /// Determine whether all provided permission enums exist within the ClaimsPrincipal's permission bits string.
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, params TEnum[] permissionEnums);
    /// <summary>
    /// Extract the permission bits string from the given ClaimsPrincipal.
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    string GetPermissionBits(ClaimsPrincipal principal);

    /// <summary>
    /// Get the permission bits string for a super administrator (all bits set to 1).
    /// </summary>
    /// <returns></returns>
    string GetAdminPermissionBits();

    /// <summary>
    /// Get the claim representing super administrator permissions.
    /// </summary>
    Claim GetAdminClaim();

    /// <summary>
    /// Convert the provided permissions into a permission bits string.
    /// </summary>
    /// <returns></returns>
    string ToPermissionBits(List<TEnum> permissionEnums);

    /// <summary>
    /// Return the list of permission enums granted by the specified bit string.
    /// </summary>
    /// <param name="permissionBits"></param>
    /// <returns></returns>
    List<TEnum> GrantedList(string permissionBits);
    /// <summary>
    /// Return the list of permissions granted to the specified ClaimsPrincipal.
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(ClaimsPrincipal principal);
    /// <summary>
    /// Return the list of granted permission enums within the provided scope from the bit string.
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(string permissionBits, params TEnum[] permissionScope);
    /// <summary>
    /// Return the list of granted permission enums within the provided scope from a ClaimsPrincipal.
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(ClaimsPrincipal principal, params TEnum[] permissionScope);
    /// <summary>
    /// Convert the specified permissions into a Claim.
    /// </summary>

    /// <returns></returns>
    Claim ToClaim(List<TEnum> permissionEnums);

    /// <summary>
    /// Get the metadata for all permission bits.
    /// </summary>
    /// <returns></returns>
    Dictionary<TEnum, IPermissionBitData> GetAllBitData();

    /// <summary>
    /// Get the metadata for a permission enum by name.
    /// </summary>
    /// <param name="key">Permission enum name</param>
    /// <returns></returns>
    (TEnum, IPermissionBitData)? GetBitData(string key);

    /// <summary>
    /// Get the metadata for the specified permission enum.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IPermissionBitData? GetBitData(TEnum key);
}
