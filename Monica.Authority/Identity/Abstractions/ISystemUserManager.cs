using System.Security.Claims;
using Monica.Authority.Identity.Models;

namespace Monica.Authority.Identity.Abstractions;

/// <summary>
/// Interface for managing the current system user
/// </summary>
public interface ISystemUserManager
{
    /// <summary>
    /// Determines whether the current user information represents a system user
    /// </summary>
    /// <param name="userInfo"></param>
    /// <returns></returns>
    public bool IsSystemUser(IAuthorityUser userInfo);
    /// <summary>
    /// Gets the token of the current system user
    /// </summary>
    /// <returns></returns>
    public string GetTokenOfCurSystemUser();
    /// <summary>
    /// Gets the claims of the current system user
    /// </summary>
    /// <returns></returns>
    public List<Claim> GetCurSystemUserClaims();
    /// <summary>
    /// Gets the principal of the current system user
    /// </summary>
    /// <returns></returns>
    public ClaimsPrincipal GetCurSystemUserPrinciple();
    SystemUserOptions.SystemUserInfo GetSystemUserInfo<T>(T userEnum) where T : struct, Enum;
    string GetTokenOfSystemUser<T>(T userEnum) where T : struct, Enum;
    List<Claim> GetSystemUserClaims<T>(T userEnum) where T : struct, Enum;
    ClaimsPrincipal GetSystemUserPrinciple<T>(T userEnum) where T : struct, Enum;
    IEnumerable<SystemUserOptions.SystemUserInfo> GetAllSystemUserInfos();
}
